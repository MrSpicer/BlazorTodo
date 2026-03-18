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
}

/// <summary>
/// Result of an import operation.
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public string? ErrorMessage { get; set; }
}
