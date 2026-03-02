using System.Text.Json;
using TodoList.Models;
using TodoList.Data;

namespace TodoList.Services;

/// <summary>
/// Implementation of IImportExportService for importing and exporting todo data.
/// </summary>
public class ImportExportService : IImportExportService
{
    private readonly ITodoRepository _repository;
    private readonly ITodoService _todoService;
    private readonly IProjectService _projectService;
    private readonly INoteService _noteService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ImportExportService(ITodoRepository repository, ITodoService todoService, IProjectService projectService, INoteService noteService)
    {
        _repository = repository;
        _todoService = todoService;
        _projectService = projectService;
        _noteService = noteService;
    }

    public async Task<string> ExportToJsonAsync()
    {
        var todos = await _repository.GetTodos();
        var exportData = new TodoExportData
        {
            ExportedAt = DateTime.Now,
            Version = "1.1",
            Projects = _projectService.Projects.ToList(),
            Todos = todos,
            Notes = _noteService.Notes.ToList()
        };

        return JsonSerializer.Serialize(exportData, JsonOptions);
    }

    public async Task<ImportResult> ImportFromJsonAsync(string json, bool replaceExisting = false)
    {
        try
        {
            var importData = JsonSerializer.Deserialize<TodoExportData>(json, JsonOptions);

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
                if (replaceExisting || !existingProjectIds.Contains(project.Id))
                    await _projectService.SaveProjectAsync(project);
            }

            // Import todos
            var existingTodos = await _repository.GetTodos();
            var existingIds = existingTodos.Select(t => t.Id).ToHashSet();

            int imported = 0;
            int skipped = 0;

            foreach (var todo in importData.Todos ?? new List<TodoItem>())
            {
                if (!replaceExisting && existingIds.Contains(todo.Id))
                {
                    skipped++;
                    continue;
                }

                if (todo.Id == Guid.Empty)
                    todo.Id = Guid.NewGuid();

                await _todoService.SaveTodoAsync(todo);
                imported++;
            }

            // Import notes
            var existingNoteIds = _noteService.Notes.Select(n => n.Id).ToHashSet();
            foreach (var note in importData.Notes ?? new List<ProjectNote>())
            {
                if (note.Id == Guid.Empty)
                    note.Id = Guid.NewGuid();
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
            return new ImportResult
            {
                Success = false,
                ErrorMessage = $"Invalid JSON format: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = $"Import failed: {ex.Message}"
            };
        }
    }
}

/// <summary>
/// Data structure for export/import.
/// </summary>
public class TodoExportData
{
    public DateTime ExportedAt { get; set; }
    public string Version { get; set; } = "1.1";
    public List<Project> Projects { get; set; } = new();
    public List<TodoItem> Todos { get; set; } = new();
    public List<ProjectNote> Notes { get; set; } = new();
}
