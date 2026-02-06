using TodoList.Models.Enums;

namespace TodoList.Models;

/// <summary>
/// Encapsulates all filter criteria for filtering todo items.
/// </summary>
public class TodoFilterCriteria
{
	/// <summary>
	/// Text to search for in todo titles and descriptions (case-insensitive).
	/// </summary>
	public string SearchText { get; set; } = string.Empty;

	/// <summary>
	/// Selected priorities to filter by. Empty list means no priority filtering.
	/// </summary>
	public List<Priority> SelectedPriorities { get; set; } = new();

	/// <summary>
	/// Selected statuses to filter by. Empty list means no status filtering.
	/// </summary>
	public List<TodoItemStatus> SelectedStatuses { get; set; } = new();

	/// <summary>
	/// Sort option for ordering results.
	/// </summary>
	public SortOption Sort { get; set; } = SortOption.CreatedDate;

	/// <summary>
	/// Gets whether any filters are currently active (excluding sort).
	/// </summary>
	public bool HasActiveFilters =>
		!string.IsNullOrWhiteSpace(SearchText) ||
		SelectedPriorities.Any() ||
		SelectedStatuses.Any();

	/// <summary>
	/// Clears all filter criteria (resets to default state).
	/// </summary>
	public void Clear()
	{
		SearchText = string.Empty;
		SelectedPriorities.Clear();
		SelectedStatuses.Clear();
	}

	/// <summary>
	/// Toggles a priority in the selected priorities list.
	/// </summary>
	public void TogglePriority(Priority priority)
	{
		if (SelectedPriorities.Contains(priority))
			SelectedPriorities.Remove(priority);
		else
			SelectedPriorities.Add(priority);
	}

	/// <summary>
	/// Toggles a status in the selected statuses list.
	/// </summary>
	public void ToggleStatus(TodoItemStatus status)
	{
		if (SelectedStatuses.Contains(status))
			SelectedStatuses.Remove(status);
		else
			SelectedStatuses.Add(status);
	}
}
