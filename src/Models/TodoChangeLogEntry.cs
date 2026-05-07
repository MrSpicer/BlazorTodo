namespace TodoList.Models;

public class TodoChangeLogEntry
{
	public DateTime ChangedAt { get; set; } = DateTime.Now;
	public string Field { get; set; } = string.Empty;
	public string? OldValue { get; set; }
	public string? NewValue { get; set; }
}
