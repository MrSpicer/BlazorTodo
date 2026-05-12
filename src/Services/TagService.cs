using TodoList.Data;
using TodoList.Models;

namespace TodoList.Services;

public class TagService : EntityServiceBase<Tag>, ITagService
{
	private readonly ITagRepository _tagRepository;
	private readonly ITodoRepository _todoRepository;

	public event Action? OnTagsChanged
	{
		add => OnChanged += value;
		remove => OnChanged -= value;
	}

	public IReadOnlyList<Tag> Tags => Items;

	public TagService(ITagRepository tagRepository, ITodoRepository todoRepository, ILogger<TagService> logger)
		: base((IRepository<Tag>)tagRepository, logger)
	{
		_tagRepository = tagRepository;
		_todoRepository = todoRepository;
	}

	public override async Task InitializeAsync()
	{
		await Repository.InitializeAsync();
		await _todoRepository.InitializeAsync();
		_items = await Repository.GetAll();
		NotifyChanged();
	}

	public async Task<Tag> GetOrCreateAsync(string name)
	{
		var trimmed = (name ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(trimmed))
			throw new ArgumentException("Tag name is required", nameof(name));

		var existing = _items.FirstOrDefault(t => string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase));
		if (existing != null)
			return existing;

		var tag = new Tag { Name = trimmed };
		var ok = await Repository.AddOrUpdate(tag);
		if (ok)
		{
			_items.Add(tag);
			NotifyChanged();
		}
		return tag;
	}

	public IEnumerable<Tag> Search(string query)
	{
		if (string.IsNullOrWhiteSpace(query))
			return _items.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase);
		return _items
			.Where(t => t.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
			.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase);
	}

	public async Task<bool> UpdateAsync(Tag tag)
	{
		if (tag == null)
			return false;

		var trimmed = (tag.Name ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(trimmed))
			return false;

		var conflict = _items.Any(t => t.Id != tag.Id
			&& string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase));
		if (conflict)
			return false;

		tag.Name = trimmed;
		var ok = await Repository.AddOrUpdate(tag);
		if (!ok)
			return false;

		var index = _items.FindIndex(t => t.Id == tag.Id);
		if (index >= 0)
			_items[index] = tag;
		else
			_items.Add(tag);

		NotifyChanged();
		return true;
	}

	public async Task<int> GetUsageCountAsync(Guid tagId)
	{
		var todos = await _todoRepository.GetTodos();
		var count = 0;
		foreach (var todo in todos)
		{
			if (todo.TagIds.Contains(tagId))
				count++;
			foreach (var sub in todo.SubTasks)
			{
				if (sub.TagIds.Contains(tagId))
					count++;
			}
		}
		return count;
	}

	public async Task DeleteAsync(Tag tag)
	{
		var deletedId = tag.Id;
		var now = DateTime.Now;

		await Repository.Delete(tag);

		var todos = await _todoRepository.GetTodos();
		foreach (var todo in todos)
		{
			var oldTopNames = FormatTagNames(todo.TagIds);
			var topRemoved = todo.TagIds.Remove(deletedId);

			var subChanged = false;
			foreach (var sub in todo.SubTasks)
			{
				if (sub.TagIds.Remove(deletedId))
					subChanged = true;
			}

			if (topRemoved || subChanged)
			{
				todo.UpdatedAt = now;
				if (topRemoved)
				{
					todo.ChangeLog.Add(new TodoChangeLogEntry
					{
						ChangedAt = now,
						Field = "TagIds",
						OldValue = oldTopNames,
						NewValue = FormatTagNames(todo.TagIds)
					});
				}
				await _todoRepository.AddOrUpdate(todo);
			}
		}

		_items.RemoveAll(t => t.Id == deletedId);
		NotifyChanged();
	}

	private string FormatTagNames(List<Guid> ids)
	{
		if (ids.Count == 0)
			return string.Empty;
		var names = ids
			.Select(id => _items.FirstOrDefault(t => t.Id == id)?.Name ?? id.ToString("N").Substring(0, 6))
			.OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
		return string.Join(", ", names);
	}
}
