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
	private readonly IPriorityService _priorityService;
	private readonly AppDataSerializer _serializer;
	private readonly ILogger<ImportExportService> _logger;
	private bool _servicesInitialized;

	public ImportExportService(
		ITodoRepository repository,
		ITodoService todoService,
		IProjectService projectService,
		INoteService noteService,
		ITagService tagService,
		IStatusService statusService,
		IPriorityService priorityService,
		AppDataSerializer serializer,
		ILogger<ImportExportService> logger)
	{
		_repository = repository;
		_todoService = todoService;
		_projectService = projectService;
		_noteService = noteService;
		_tagService = tagService;
		_statusService = statusService;
		_priorityService = priorityService;
		_serializer = serializer;
		_logger = logger;
	}

	// Export/import both read the services' in-memory caches, which are only populated by each
	// service's InitializeAsync(). A fresh circuit landing directly on /settings (without first
	// visiting a page that initializes them) would otherwise export/dedup against empty caches.
	// All six InitializeAsync() calls are idempotent, so this is safe to call repeatedly.
	private async Task EnsureServicesInitializedAsync()
	{
		if (_servicesInitialized)
			return;

		await _projectService.InitializeAsync();
		await _tagService.InitializeAsync();
		await _statusService.InitializeAsync();
		await _priorityService.InitializeAsync();
		await _todoService.InitializeAsync();
		await _noteService.InitializeAsync();
		_servicesInitialized = true;
	}

	public async Task<string> ExportToJsonAsync()
	{
		await EnsureServicesInitializedAsync();
		var todos = await _repository.GetTodos();
		var document = new AppDataDocument
		{
			ExportedAt = DateTime.UtcNow,
			Version = "1.5",
			Projects = _projectService.Projects.ToList(),
			Todos = todos,
			Notes = _noteService.Notes.ToList(),
			Tags = _tagService.Tags.ToList(),
			Statuses = _statusService.Statuses.ToList(),
			Priorities = _priorityService.Priorities.ToList()
		};

		return _serializer.Serialize(document);
	}

	public async Task<ImportResult> ImportFromJsonAsync(string json, bool replaceExisting = false)
	{
		AppDataDocument? importData;
		try
		{
			importData = _serializer.Deserialize(json);
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

		if (importData == null)
			return new ImportResult { Success = false, ErrorMessage = "Invalid import data." };

		return await ImportAsync(importData, replaceExisting);
	}

	public async Task<ImportResult> ImportAsync(AppDataDocument importData, bool replaceExisting = false)
	{
		try
		{
			await EnsureServicesInitializedAsync();

			var result = new ImportResult { Success = true };

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
			int projectsRejected = 0;
			foreach (var project in importData.Projects ?? new List<Project>())
			{
				if (project.Id == Guid.Empty)
					project.Id = Guid.NewGuid();
				if (!IsValid(project))
				{
					projectsRejected++;
					continue;
				}
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

			// Import priorities. Built-ins are upserted by Guid (peer device may have edited
			// the name/color/rank); custom priorities dedupe by name (case-insensitive).
			var priorityIdRemap = new Dictionary<Guid, Guid>();
			var existingCustomPrioritiesByName = _priorityService.Priorities
				.Where(p => !p.IsBuiltIn)
				.ToDictionary(p => p.Name.Trim(), p => p, StringComparer.OrdinalIgnoreCase);
			foreach (var incoming in importData.Priorities ?? new List<Priority>())
			{
				if (incoming.Id == Guid.Empty || string.IsNullOrWhiteSpace(incoming.Name))
					continue;

				if (BuiltInPriorityIds.IsBuiltIn(incoming.Id))
				{
					incoming.IsBuiltIn = true;
					await _priorityService.UpdateAsync(incoming);
					continue;
				}

				incoming.IsBuiltIn = false;
				if (existingCustomPrioritiesByName.TryGetValue(incoming.Name.Trim(), out var existing))
				{
					if (existing.Id != incoming.Id)
						priorityIdRemap[incoming.Id] = existing.Id;
				}
				else
				{
					var added = await _priorityService.AddAsync(incoming);
					if (added)
						existingCustomPrioritiesByName[incoming.Name.Trim()] = incoming;
				}
			}

			// Every imported/existing todo and note must reference a project the user owns —
			// otherwise the EF ownership guard silently rejects the save. Build the set of valid
			// project ids (pre-existing + just-imported) and guarantee a default to catch orphans.
			var validProjectIds = _projectService.Projects.Select(p => p.Id).ToHashSet();
			var defaultProject = _projectService.GetDefaultProject() ?? _projectService.Projects.FirstOrDefault();
			if (defaultProject is null)
			{
				defaultProject = new Project { Name = "Personal", IsDefault = true };
				await _projectService.SaveProjectAsync(defaultProject);
				validProjectIds.Add(defaultProject.Id);
			}

			// Import todos. Order parents-first (nesting is one level deep) so a child's parent has
			// already landed when we validate its ParentId.
			var existingTodos = await _repository.GetTodos();
			var landedTodoIds = existingTodos.Select(t => t.Id).ToHashSet();
			var existingIds = new HashSet<Guid>(landedTodoIds);

			int imported = 0;
			int skipped = 0;
			int remapped = 0;
			int duplicates = 0;
			int reparented = 0;

			var incomingTodos = (importData.Todos ?? new List<TodoItem>())
				.OrderBy(t => t.ParentId.HasValue ? 1 : 0)
				.ToList();

			foreach (var todo in incomingTodos)
			{
				if (!replaceExisting && existingIds.Contains(todo.Id))
				{
					duplicates++;
					continue;
				}

				if (todo.Id == Guid.Empty)
					todo.Id = Guid.NewGuid();

				// Orphaned project reference (empty, or a project that failed validation / isn't
				// owned) → move to the default project rather than silently dropping the todo.
				if (todo.ProjectId == Guid.Empty || !validProjectIds.Contains(todo.ProjectId))
				{
					todo.ProjectId = defaultProject.Id;
					remapped++;
				}

				// Parent hasn't landed (missing, or listed but rejected) → import as top-level.
				if (todo.ParentId is Guid parentId && !landedTodoIds.Contains(parentId))
				{
					todo.ParentId = null;
					reparented++;
				}

				if (!IsValid(todo))
				{
					skipped++;
					continue;
				}

				RemapTagIds(todo, tagIdRemap);
				FillStatusId(todo, statusIdRemap);
				FillPriorityId(todo, priorityIdRemap);

				// Defensive flatten for legacy (≤ v1.4) exports — children arrive nested
				// inside the parent. Hoist them to top-level rows with ParentId set.
				// v1.5+ exports already write a flat list and children come through the
				// outer loop on their own.
				var legacyChildren = todo.SubTasks.ToList();
				todo.SubTasks.Clear();

				if (await _todoService.SaveTodoAsync(todo))
				{
					imported++;
					landedTodoIds.Add(todo.Id);
				}
				else
				{
					skipped++;
					continue;
				}

				foreach (var sub in legacyChildren)
				{
					sub.ParentId = todo.Id;
					sub.ProjectId = todo.ProjectId;
					if (sub.Id == Guid.Empty)
						sub.Id = Guid.NewGuid();
					if (!replaceExisting && existingIds.Contains(sub.Id))
					{
						duplicates++;
						continue;
					}
					if (sub.ProjectId == Guid.Empty || !validProjectIds.Contains(sub.ProjectId))
					{
						sub.ProjectId = defaultProject.Id;
						remapped++;
					}
					if (!IsValid(sub))
					{
						skipped++;
						continue;
					}
					RemapTagIds(sub, tagIdRemap);
					FillStatusId(sub, statusIdRemap);
					FillPriorityId(sub, priorityIdRemap);
					if (await _todoService.SaveTodoAsync(sub))
					{
						imported++;
						landedTodoIds.Add(sub.Id);
					}
					else
						skipped++;
				}
			}

			// Import notes — same orphan-project remap; count save failures.
			var existingNoteIds = _noteService.Notes.Select(n => n.Id).ToHashSet();
			foreach (var note in importData.Notes ?? new List<ProjectNote>())
			{
				if (note.Id == Guid.Empty)
					note.Id = Guid.NewGuid();

				if (note.ProjectId == Guid.Empty || !validProjectIds.Contains(note.ProjectId))
				{
					note.ProjectId = defaultProject.Id;
					remapped++;
				}

				if (!IsValid(note))
				{
					skipped++;
					continue;
				}

				if (!replaceExisting && existingNoteIds.Contains(note.Id))
				{
					duplicates++;
					continue;
				}

				if (await _noteService.SaveNoteAsync(note))
					imported++;
				else
					skipped++;
			}

			result.ImportedCount = imported;
			result.SkippedCount = skipped;
			result.RemappedToDefaultCount = remapped;

			if (duplicates > 0)
				result.Warnings.Add($"{duplicates} item(s) already existed and were skipped.");
			if (reparented > 0)
				result.Warnings.Add($"{reparented} sub-task(s) referenced a missing parent and were imported as top-level items.");
			if (projectsRejected > 0)
				result.Warnings.Add($"{projectsRejected} project(s) could not be imported; their items were moved to the default project.");

			return result;
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

	// Mirrors FillStatusId for priorities. Pre-v1.4 exports populate only the legacy enum;
	// v1.4 exports may carry a custom-priority Guid that was deduped on this device.
	private static void FillPriorityId(TodoItem todo, Dictionary<Guid, Guid> priorityIdRemap)
	{
		if (todo.PriorityId == Guid.Empty)
			todo.PriorityId = BuiltInPriorityIds.FromLegacyEnum((int)todo.Priority);
		else if (priorityIdRemap.TryGetValue(todo.PriorityId, out var mapped))
			todo.PriorityId = mapped;
	}
}
