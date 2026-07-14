using TodoList.Models;

namespace TodoList.Services;

/// <summary>
/// Service for importing and exporting todo data.
/// </summary>
public interface IImportExportService
{
    /// <summary>
    /// Exports all todos to a JSON string.
    /// </summary>
    Task<string> ExportToJsonAsync();

    /// <summary>
    /// Imports todos from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string containing todo data.</param>
    /// <param name="replaceExisting">If true, replaces all existing todos. If false, merges with existing.</param>
    /// <returns>The number of todos imported.</returns>
    Task<ImportResult> ImportFromJsonAsync(string json, bool replaceExisting = false);

    /// <summary>
    /// Imports an already-deserialized document. Shared merge core used by both JSON import
    /// and the local-data-to-account migration path.
    /// </summary>
    /// <param name="document">The document to import.</param>
    /// <param name="replaceExisting">If true, replaces all existing data. If false, merges with existing.</param>
    Task<ImportResult> ImportAsync(AppDataDocument document, bool replaceExisting = false);
}

/// <summary>
/// Result of an import operation.
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }

    /// <summary>Todos/notes whose project reference was invalid and were moved to the default project.</summary>
    public int RemappedToDefaultCount { get; set; }

    /// <summary>Non-fatal notices (duplicates skipped, orphans re-parented, projects rejected, …).</summary>
    public List<string> Warnings { get; set; } = new();

    public string? ErrorMessage { get; set; }
}
