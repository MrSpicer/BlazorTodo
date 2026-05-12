using System.Text.Json;
using TodoList.Models;

namespace TodoList.Services;

/// <summary>
/// Pure JSON ↔ DTO transform for app data export/import.
/// Holds no state and depends on no other services — makes serialization concerns
/// testable in isolation and lets <see cref="ImportExportService"/> focus on orchestration.
/// </summary>
public class AppDataSerializer
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public string Serialize(AppDataDocument document) =>
		JsonSerializer.Serialize(document, JsonOptions);

	public AppDataDocument? Deserialize(string json) =>
		JsonSerializer.Deserialize<AppDataDocument>(json, JsonOptions);
}

/// <summary>
/// Wire-format DTO for export/import. Mirrors the previous <c>TodoExportData</c> shape;
/// existing exports remain readable.
/// </summary>
public class AppDataDocument
{
	public DateTime ExportedAt { get; set; }
	public string Version { get; set; } = "1.3";
	public List<Project> Projects { get; set; } = new();
	public List<TodoItem> Todos { get; set; } = new();
	public List<ProjectNote> Notes { get; set; } = new();
	public List<Tag> Tags { get; set; } = new();
	public List<Status> Statuses { get; set; } = new();
}
