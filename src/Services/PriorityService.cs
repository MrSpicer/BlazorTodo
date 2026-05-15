using TodoList.Data;
using TodoList.Models;

namespace TodoList.Services;

public class PriorityService : EntityServiceBase<Priority>, IPriorityService
{
	private readonly IPriorityRepository _priorityRepository;
	private readonly ITodoRepository _todoRepository;
	private bool _initialized;

	public event Action? OnPrioritiesChanged
	{
		add => OnChanged += value;
		remove => OnChanged -= value;
	}

	public IReadOnlyList<Priority> Priorities => Items;

	public PriorityService(IPriorityRepository priorityRepository, ITodoRepository todoRepository, ILogger<PriorityService> logger)
		: base((IRepository<Priority>)priorityRepository, logger)
	{
		_priorityRepository = priorityRepository;
		_todoRepository = todoRepository;
		_items = BuiltInPriorityIds.Seed().ToList();
	}

	public override async Task InitializeAsync()
	{
		if (_initialized)
			return;

		await Repository.InitializeAsync();
		var stored = await Repository.GetAll();

		// Built-ins live in-memory only (seeded from BuiltInPriorityIds.Seed). Dedupe stored
		// rows that share a built-in Id (legacy LocalStorage may have copies) so we don't
		// show duplicates and so we don't try to re-persist built-ins to a per-user DB.
		var seeded = BuiltInPriorityIds.Seed();
		var customStored = stored.Where(s => !BuiltInPriorityIds.IsBuiltIn(s.Id)).ToList();
		_items = seeded.Concat(customStored).ToList();
		_initialized = true;
		NotifyChanged();
	}

	public async Task<bool> AddAsync(Priority priority)
	{
		if (priority is null)
			return false;

		var trimmed = (priority.Name ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(trimmed))
			return false;

		var conflict = _items.Any(p => string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase));
		if (conflict)
			return false;

		priority.Name = trimmed;
		if (priority.Id == Guid.Empty)
			priority.Id = Guid.NewGuid();
		priority.IsBuiltIn = false;

		var ok = await Repository.AddOrUpdate(priority);
		if (!ok)
			return false;

		_items.Add(priority);
		NotifyChanged();
		return true;
	}

	public async Task<bool> UpdateAsync(Priority priority)
	{
		if (priority == null)
			return false;

		var trimmed = (priority.Name ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(trimmed))
			return false;

		var conflict = _items.Any(p => p.Id != priority.Id
			&& string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase));
		if (conflict)
			return false;

		var existing = _items.FirstOrDefault(p => p.Id == priority.Id);
		if (existing != null)
			priority.IsBuiltIn = existing.IsBuiltIn;

		priority.Name = trimmed;

		// Built-in priorities are in-memory only — apply edits to the local list but skip persistence.
		if (!priority.IsBuiltIn)
		{
			var ok = await Repository.AddOrUpdate(priority);
			if (!ok)
				return false;
		}

		var index = _items.FindIndex(p => p.Id == priority.Id);
		if (index >= 0)
			_items[index] = priority;
		else
			_items.Add(priority);

		NotifyChanged();
		return true;
	}

	public async Task<int> GetUsageCountAsync(Guid priorityId)
	{
		var todos = await _todoRepository.GetTodos();
		return todos.Count(t => t.PriorityId == priorityId);
	}

	public async Task<bool> DeleteAsync(Priority priority)
	{
		if (priority is null)
			return false;

		if (BuiltInPriorityIds.IsBuiltIn(priority.Id))
		{
			Logger.LogWarning("Refusing to delete built-in priority {Id}", priority.Id);
			return false;
		}

		var deletedId = priority.Id;
		var now = DateTime.Now;

		await Repository.Delete(priority);

		var todos = await _todoRepository.GetTodos();
		foreach (var todo in todos)
		{
			if (todo.PriorityId == deletedId)
			{
				todo.PriorityId = BuiltInPriorityIds.Medium;
				todo.UpdatedAt = now;
				await _todoRepository.AddOrUpdate(todo);
			}
		}

		_items.RemoveAll(p => p.Id == deletedId);
		NotifyChanged();
		return true;
	}
}
