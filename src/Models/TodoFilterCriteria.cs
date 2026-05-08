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
	/// Selected status ids to filter by. Empty list means no status filtering.
	/// </summary>
	public List<Guid> SelectedStatuses { get; set; } = BuiltInStatusIds.DefaultFilterIds.ToList();

	/// <summary>
	/// Ordered list of sort criteria applied in sequence.
	/// </summary>
	// Status asc (sensible default), Priority asc (lowest first)
	public List<SortCriterion> SortCriteria { get; set; } =
		[new(SortOption.Status, true), new(SortOption.Priority, true)];

	public IEnumerable<SortOption> AvailableSortOptions =>
		Enum.GetValues<SortOption>().Except(SortCriteria.Select(c => c.Option));

	private static readonly HashSet<Guid> _defaultStatuses = BuiltInStatusIds.DefaultFilterIds.ToHashSet();

	/// <summary>
	/// Gets whether any filters are currently active (excluding sort).
	/// </summary>
	public bool HasActiveFilters =>
		!string.IsNullOrWhiteSpace(SearchText) ||
		SelectedPriorities.Any() ||
		!new HashSet<Guid>(SelectedStatuses).SetEquals(_defaultStatuses);

	/// <summary>
	/// Restores default sort criteria (Status asc, Priority asc).
	/// </summary>
	public void RestoreDefaultSorts()
	{
		SortCriteria = [new(SortOption.Status, true), new(SortOption.Priority, true)];
	}

	/// <summary>
	/// Clears all filter criteria (resets to default state).
	/// </summary>
	public void Clear()
	{
		SearchText = string.Empty;
		SelectedPriorities.Clear();
		SelectedStatuses = BuiltInStatusIds.DefaultFilterIds.ToList();
	}

	/// <summary>
	/// Replaces all filter values with those from the given preset.
	/// </summary>
	public void ApplyPreset(FilterPreset preset)
	{
		SearchText = preset.SearchText;
		SelectedPriorities = preset.SelectedPriorities.ToList();
		SelectedStatuses = preset.SelectedStatuses.ToList();
		SortCriteria = preset.SortCriteria.ToList();
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
	/// Toggles a status id in the selected statuses list.
	/// </summary>
	public void ToggleStatus(Guid statusId)
	{
		if (SelectedStatuses.Contains(statusId))
			SelectedStatuses.Remove(statusId);
		else
			SelectedStatuses.Add(statusId);
	}
}
