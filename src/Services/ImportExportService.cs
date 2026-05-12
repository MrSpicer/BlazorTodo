using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using TodoList.Models;
using TodoList.Data;

namespace TodoList.Services;

/// <summary>
/// Orchestrates import/export of app data. Serialization is delegated to
/// <see cref="AppDataSerializer"/>; this service handles the cross-entity logic:
/// tag dedup-and-remap, status dedup-and-remap, project/todo/note upserts.
/// </summary>
public class ImportExportService : IImportExportService
{
	private readonly ITodoRepository _repository;
	private readonly ITodoService _todoService;
	private readonly IProjectService _projectService;
	private readonly INoteService _noteService;
	private readonly ITagService _tagService;
	private readonly IStatusService _statusService;
	private readonly AppDataSerializer _serializer;
	private readonly ILogger<ImportExportService> _logger;

	public ImportExportService(
		ITodoRepository repository,
		ITodoService todoService,
		IProjectService projectService,
		INoteService noteService,
		ITagService tagService,
		IStatusService statusService,
		AppDataSerializer serializer,
		ILogger<ImportExportService> logger)
	{
		_repository = repository;
		_todoService = todoService;
		_projectService = projectService;
		_noteService = noteService;
		_tagService = tagService;
		_statusService = statusService;
		_serializer = serializer;
		_logger = logger;
	}

	public async Task<string> ExportToJsonAsync()
	{
		var todos = await _repository.GetTodos();
		var document = new AppDataDocument
		{
			ExportedAt = DateTime.Now,
			Version = "1.3",
			Projects = _projectService.Projects.ToList(),
			Todos = todos,
			Notes = _noteService.Notes.ToList(),
			Tags = _tagService.Tags.ToList(),
			Statuses = _statusService.Statuses.ToList()
		};

		return _serializer.Serialize(document);
	}

	public async Task<ImportResult> ImportFromJsonAsync(string json, bool replaceExisting = false)
	{
		try
		{
			var importData = _serializer.Deserialize(json);

			if (importData == null)
			{
				return new ImportResult { Success = false, ErrorMessage = "Invalid import data." };
			}

			if (replaceExisting)
			{
				foreach (var project in _projectService.Projects.ToList())
					await _projectService.DeleteProjectAsync(project);
				await _todoService.ClearAllAsync();
				foreach (var note in _noteService.Notes.ToList())
					await _noteService.DeleteNoteAsync(note);
			}

			// Import projects
			var existingProjectIds = _projectService.Projects.Select(p => p.Id).ToHashSet();
			foreach (var project in importData.Projects ?? new List<Project>())
			{
				if (project.Id == Guid.Empty)
					project.Id = Guid.NewGuid();
				if (!IsValid(project))
					continue;
				if (replaceExisting || !existingProjectIds.Contains(project.Id))
					await _projectService.SaveProjectAsync(project);
			}

			// Import tags first (todos reference them) and build a remap from incoming tag id → local tag id.
			// Tags are deduped case-insensitively by name across all devices/projects.
			var tagIdRemap = new Dictionary<Guid, Guid>();
			var existingTagsByName = _tagService.Tags.ToDictionary(t => t.Name.Trim(), t => t, StringComparer.OrdinalIgnoreCase);
			foreach (var tag in importData.Tags ?? new List<Tag>())
			{
				if (tag.Id == Guid.Empty)
					tag.Id = Guid.NewGuid();
				if (!IsValid(tag))
					continue;

				if (existingTagsByName.TryGetValue(tag.Name.Trim(), out var existingTag))
				{
					if (existingTag.Id != tag.Id)
						tagIdRemap[tag.Id] = existingTag.Id;
				}
				else
				{
					var created = await _tagService.GetOrCreateAsync(tag.Name);
					if (created.Id != tag.Id)
						tagIdRemap[tag.Id] = created.Id;
					existingTagsByName[created.Name] = created;
				}
			}

			// Import statuses. Built-ins are upserted by Guid (peer device may have edited
			// the name/color/emoji); custom statuses dedupe by name (case-insensitive).
			var statusIdRemap = new Dictionary<Guid, Guid>();
			var existingCustomStatusesByName = _statusService.Statuses
				.Where(s => !s.IsBuiltIn)
				.ToDictionary(s => s.Name.Trim(), s => s, StringComparer.OrdinalIgnoreCase);
			foreach (var incoming in importData.Statuses ?? new List<Status>())
			{
				if (incoming.Id == Guid.Empty || string.IsNullOrWhiteSpace(incoming.Name))
					continue;

				if (BuiltInStatusIds.IsBuiltIn(incoming.Id))
				{
					incoming.IsBuiltIn = true;
					await _statusService.UpdateAsync(incoming);
					continue;
				}

				incoming.IsBuiltIn = false;
				if (existingCustomStatusesByName.TryGetValue(incoming.Name.Trim(), out var existing))
				{
					if (existing.Id != incoming.Id)
						statusIdRemap[incoming.Id] = existing.Id;
				}
				else
				{
					var added = await _statusService.AddAsync(incoming);
					if (added)
						existingCustomStatusesByName[incoming.Name.Trim()] = incoming;
				}
			}

			// Import todos
			var existingTodos = await _repository.GetTodos();
			var existingIds = existingTodos.Select(t => t.Id).ToHashSet();

			int imported = 0;
			int skipped = 0;

			var importedAt = DateTime.Now;

			foreach (var todo in importData.Todos ?? new List<TodoItem>())
			{
				if (!replaceExisting && existingIds.Contains(todo.Id))
				{
					skipped++;
					continue;
				}

				if (todo.Id == Guid.Empty)
					todo.Id = Guid.NewGuid();

				if (!IsValid(todo))
				{
					skipped++;
					continue;
				}

				RemapTagIds(todo, tagIdRemap);
				FillStatusId(todo, statusIdRemap);
				todo.LastSyncedAt = importedAt;
				foreach (var sub in todo.SubTasks)
				{
					RemapTagIds(sub, tagIdRemap);
					FillStatusId(sub, statusIdRemap);
					sub.LastSyncedAt = importedAt;
				}

				await _todoService.SaveTodoAsync(todo);
				imported++;
			}

			// Import notes
			var existingNoteIds = _noteService.Notes.Select(n => n.Id).ToHashSet();
			foreach (var note in importData.Notes ?? new List<ProjectNote>())
			{
				if (note.Id == Guid.Empty)
					note.Id = Guid.NewGuid();
				if (!IsValid(note))
					continue;
				if (replaceExisting || !existingNoteIds.Contains(note.Id))
					await _noteService.SaveNoteAsync(note);
			}

			return new ImportResult
			{
				Success = true,
				ImportedCount = imported,
				SkippedCount = skipped
			};
		}
		catch (JsonException ex)
		{
			_logger.LogWarning(ex, "Import failed: invalid JSON");
			return new ImportResult
			{
				Success = false,
				ErrorMessage = "Invalid JSON format. Please check the file and try again."
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Import failed");
			return new ImportResult
			{
				Success = false,
				ErrorMessage = "Import failed due to an unexpected error."
			};
		}
	}

	private static bool IsValid<T>(T entity) where T : class
	{
		var context = new ValidationContext(entity);
		return Validator.TryValidateObject(entity, context, null, validateAllProperties: true);
	}

	private static void RemapTagIds(TodoItem todo, Dictionary<Guid, Guid> remap)
	{
		if (remap.Count == 0 || todo.TagIds is null || todo.TagIds.Count == 0)
			return;
		for (int i = 0; i < todo.TagIds.Count; i++)
		{
			if (remap.TryGetValue(todo.TagIds[i], out var mapped))
				todo.TagIds[i] = mapped;
		}
	}

	// Ensures incoming TodoItem has a non-empty StatusId. Pre-v1.3 exports populate only
	// the legacy enum field, so we map it to a built-in Guid. v1.3 exports may carry a
	// custom-status Guid that was deduped on this device — apply the remap.
	private static void FillStatusId(TodoItem todo, Dictionary<Guid, Guid> statusIdRemap)
	{
		if (todo.StatusId == Guid.Empty)
			todo.StatusId = BuiltInStatusIds.FromLegacyEnum((int)todo.Status);
		else if (statusIdRemap.TryGetValue(todo.StatusId, out var mapped))
			todo.StatusId = mapped;
	}
}
