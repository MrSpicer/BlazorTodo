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

		var seeded = BuiltInPriorityIds.Seed();
		var missing = seeded.Where(s => stored.All(x => x.Id != s.Id)).ToList();
		foreach (var p in missing)
			await Repository.AddOrUpdate(p);

		if (missing.Count > 0)
			stored.AddRange(missing);

		_items = stored;
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
		var ok = await Repository.AddOrUpdate(priority);
		if (!ok)
			return false;

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
		var count = 0;
		foreach (var todo in todos)
		{
			if (todo.PriorityId == priorityId)
				count++;
			foreach (var sub in todo.SubTasks)
			{
				if (sub.PriorityId == priorityId)
					count++;
			}
		}
		return count;
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
			var changed = false;

			if (todo.PriorityId == deletedId)
			{
				todo.PriorityId = BuiltInPriorityIds.Medium;
				changed = true;
			}

			foreach (var sub in todo.SubTasks)
			{
				if (sub.PriorityId == deletedId)
				{
					sub.PriorityId = BuiltInPriorityIds.Medium;
					changed = true;
				}
			}

			if (changed)
			{
				todo.UpdatedAt = now;
				await _todoRepository.AddOrUpdate(todo);
			}
		}

		_items.RemoveAll(p => p.Id == deletedId);
		NotifyChanged();
		return true;
	}
}
