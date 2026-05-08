using TodoList.Models.Enums;

namespace TodoList.Models;

public record FilterPreset(
	string Name,
	string SearchText,
	List<Priority> SelectedPriorities,
	List<Guid> SelectedStatuses,
	List<SortCriterion> SortCriteria)
{
	public const string DefaultName = "Default";

	public static FilterPreset SystemDefault => new(
		DefaultName,
		string.Empty,
		new List<Priority>(),
		BuiltInStatusIds.DefaultFilterIds.ToList(),
		new List<SortCriterion>
		{
			new(SortOption.Status, true),
			new(SortOption.Priority, true),
		});

	public static FilterPreset FromCriteria(string name, TodoFilterCriteria criteria) => new(
		name,
		criteria.SearchText,
		criteria.SelectedPriorities.ToList(),
		criteria.SelectedStatuses.ToList(),
		criteria.SortCriteria.ToList());
}
