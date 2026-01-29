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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ImportExportService(ITodoRepository repository, ITodoService todoService)
    {
        _repository = repository;
        _todoService = todoService;
    }

    public async Task<string> ExportToJsonAsync()
    {
        var todos = await _repository.GetTodos();
        var exportData = new TodoExportData
        {
            ExportedAt = DateTime.Now,
            Version = "1.0",
            Todos = todos
        };

        return JsonSerializer.Serialize(exportData, JsonOptions);
    }

    public async Task<ImportResult> ImportFromJsonAsync(string json, bool replaceExisting = false)
    {
        try
        {
            var importData = JsonSerializer.Deserialize<TodoExportData>(json, JsonOptions);

            if (importData?.Todos == null || !importData.Todos.Any())
            {
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = "No todos found in the import file."
                };
            }

            if (replaceExisting)
            {
                await _todoService.ClearAllAsync();
            }

            var existingTodos = await _repository.GetTodos();
            var existingIds = existingTodos.Select(t => t.Id).ToHashSet();

            int imported = 0;
            int skipped = 0;

            foreach (var todo in importData.Todos)
            {
                if (!replaceExisting && existingIds.Contains(todo.Id))
                {
                    skipped++;
                    continue;
                }

                // Ensure the todo has a valid ID
                if (todo.Id == Guid.Empty)
                {
                    todo.Id = Guid.NewGuid();
                }

                await _todoService.SaveTodoAsync(todo);
                imported++;
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
    public string Version { get; set; } = "1.0";
    public List<TodoItem> Todos { get; set; } = new();
}
