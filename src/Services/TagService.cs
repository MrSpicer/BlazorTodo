using TodoList.Data;
using TodoList.Models;

namespace TodoList.Services;

public class TagService : ITagService
{
	private readonly ITagRepository _tagRepository;
	private readonly ITodoRepository _todoRepository;
	private readonly ILogger<TagService> _logger;
	private List<Tag> _tags = new();

	public event Action? OnTagsChanged;
	public IReadOnlyList<Tag> Tags => _tags.AsReadOnly();

	public TagService(ITagRepository tagRepository, ITodoRepository todoRepository, ILogger<TagService> logger)
	{
		_tagRepository = tagRepository;
		_todoRepository = todoRepository;
		_logger = logger;
	}

	public async Task InitializeAsync()
	{
		await _tagRepository.InitializeAsync();
		_tags = await _tagRepository.GetTags();
		Notify();
	}

	public async Task<Tag> GetOrCreateAsync(string name)
	{
		var trimmed = (name ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(trimmed))
			throw new ArgumentException("Tag name is required", nameof(name));

		var existing = _tags.FirstOrDefault(t => string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase));
		if (existing != null)
			return existing;

		var tag = new Tag { Name = trimmed };
		var ok = await _tagRepository.AddOrUpdate(tag);
		if (ok)
		{
			_tags.Add(tag);
			Notify();
		}
		return tag;
	}

	public Tag? GetById(Guid id) => _tags.FirstOrDefault(t => t.Id == id);

	public IEnumerable<Tag> Search(string query)
	{
		if (string.IsNullOrWhiteSpace(query))
			return _tags.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase);
		return _tags
			.Where(t => t.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
			.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase);
	}

	public async Task DeleteAsync(Tag tag)
	{
		await _tagRepository.Delete(tag);
		_tags.RemoveAll(t => t.Id == tag.Id);

		var todos = await _todoRepository.GetTodos();
		foreach (var todo in todos)
		{
			var changed = todo.TagIds.Remove(tag.Id);
			foreach (var sub in todo.SubTasks)
			{
				if (sub.TagIds.Remove(tag.Id))
					changed = true;
			}
			if (changed)
				await _todoRepository.AddOrUpdate(todo);
		}

		Notify();
	}

	private void Notify() => OnTagsChanged?.Invoke();
}
