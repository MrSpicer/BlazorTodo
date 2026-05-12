using TodoList.Models;

namespace TodoList.Services;

/// <summary>
/// Resolves tag/status Guids to human-readable names for TodoChangeLog entries.
/// Depends on the in-memory caches in <see cref="ITagService"/> and <see cref="IStatusService"/>;
/// callers (e.g. TodoService) inject this rather than referencing those services directly,
/// keeping TodoService free of cycles with the entity services that already touch ITodoRepository.
/// </summary>
public interface IChangeLogFormatter
{
	string StatusName(Guid id);
	string FormatTags(IEnumerable<Guid>? tagIds);
}

public class ChangeLogFormatter : IChangeLogFormatter
{
	private readonly ITagService _tagService;
	private readonly IStatusService _statusService;

	public ChangeLogFormatter(ITagService tagService, IStatusService statusService)
	{
		_tagService = tagService;
		_statusService = statusService;
	}

	public string StatusName(Guid id) =>
		_statusService.GetById(id)?.Name ?? (id == Guid.Empty ? string.Empty : id.ToString());

	public string FormatTags(IEnumerable<Guid>? tagIds)
	{
		if (tagIds is null)
			return string.Empty;
		var ids = tagIds as IReadOnlyCollection<Guid> ?? tagIds.ToList();
		if (ids.Count == 0)
			return string.Empty;
		var names = ids
			.Select(id => _tagService.GetById(id)?.Name ?? id.ToString("N").Substring(0, 6))
			.OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
		return string.Join(", ", names);
	}
}
