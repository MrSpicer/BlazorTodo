using TodoList.Data;
using TodoList.Helpers;
using TodoList.Models;

namespace TodoList.Services;

public class StatusService : EntityServiceBase<Status>, IStatusService
{
	private readonly IStatusRepository _statusRepository;
	private readonly ITodoRepository _todoRepository;
	private bool _initialized;

	public event Action? OnStatusesChanged
	{
		add => OnChanged += value;
		remove => OnChanged -= value;
	}

	public IReadOnlyList<Status> Statuses => Items;

	public StatusService(IStatusRepository statusRepository, ITodoRepository todoRepository, ILogger<StatusService> logger)
		: base((IRepository<Status>)statusRepository, logger)
	{
		_statusRepository = statusRepository;
		_todoRepository = todoRepository;
		_items = BuiltInStatusIds.Seed().ToList();
	}

	public override async Task InitializeAsync()
	{
		if (_initialized)
			return;

		await Repository.InitializeAsync();
		var stored = await Repository.GetAll();

		var seeded = BuiltInStatusIds.Seed();
		await MigrateLegacyColorsAsync(stored, seeded);

		var missing = seeded.Where(s => stored.All(x => x.Id != s.Id)).ToList();
		foreach (var s in missing)
			await Repository.AddOrUpdate(s);

		if (missing.Count > 0)
			stored.AddRange(missing);

		_items = stored;
		_initialized = true;
		NotifyChanged();
	}

	// One-time rewrite of statuses persisted under older color schemes:
	//  - Bootstrap-utility classes (`bg-primary`, `bg-success`, …)
	//  - status-* token classes (`status-info`, `status-warning`, …)
	// Both are converted to hex strings so the new full-range color picker
	// can render them via inline style. Built-ins snap back to their canonical
	// seed hex; custom statuses map to the nearest equivalent.
	private async Task MigrateLegacyColorsAsync(List<Status> stored, IReadOnlyList<Status> seeded)
	{
		foreach (var s in stored)
		{
			var legacy = string.IsNullOrEmpty(s.Color)
				|| s.Color.StartsWith("bg-", StringComparison.Ordinal)
				|| s.Color.StartsWith("status-", StringComparison.Ordinal);
			if (!legacy)
				continue;

			if (BuiltInStatusIds.IsBuiltIn(s.Id))
			{
				var canonical = seeded.FirstOrDefault(x => x.Id == s.Id);
				if (canonical != null)
				{
					s.Color = canonical.Color;
					s.IsBuiltIn = true;
				}
			}
			else
			{
				s.Color = MapLegacyColor(s.Color);
			}

			await Repository.AddOrUpdate(s);
		}
	}

	private static string MapLegacyColor(string legacy)
	{
		var token = legacy switch
		{
			"bg-primary"           => "status-primary",
			"bg-secondary"         => "status-muted",
			"bg-success"           => "status-success",
			"bg-danger"            => "status-danger",
			"bg-warning text-dark" => "status-warning",
			"bg-info"              => "status-info",
			"bg-light text-dark"   => "status-light",
			"bg-dark"              => "status-dark",
			_                      => legacy,
		};
		return StatusColor.NormalizeBackground(token);
	}

	public async Task<bool> AddAsync(Status status)
	{
		if (status is null)
			return false;

		var trimmed = (status.Name ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(trimmed))
			return false;

		var conflict = _items.Any(s => string.Equals(s.Name, trimmed, StringComparison.OrdinalIgnoreCase));
		if (conflict)
			return false;

		status.Name = trimmed;
		if (status.Id == Guid.Empty)
			status.Id = Guid.NewGuid();
		status.IsBuiltIn = false;

		var ok = await Repository.AddOrUpdate(status);
		if (!ok)
			return false;

		_items.Add(status);
		NotifyChanged();
		return true;
	}

	public async Task<bool> UpdateAsync(Status status)
	{
		if (status == null)
			return false;

		var trimmed = (status.Name ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(trimmed))
			return false;

		var conflict = _items.Any(s => s.Id != status.Id
			&& string.Equals(s.Name, trimmed, StringComparison.OrdinalIgnoreCase));
		if (conflict)
			return false;

		var existing = _items.FirstOrDefault(s => s.Id == status.Id);
		if (existing != null)
			status.IsBuiltIn = existing.IsBuiltIn;

		status.Name = trimmed;
		var ok = await Repository.AddOrUpdate(status);
		if (!ok)
			return false;

		var index = _items.FindIndex(s => s.Id == status.Id);
		if (index >= 0)
			_items[index] = status;
		else
			_items.Add(status);

		NotifyChanged();
		return true;
	}

	public async Task<int> GetUsageCountAsync(Guid statusId)
	{
		var todos = await _todoRepository.GetTodos();
		var count = 0;
		foreach (var todo in todos)
		{
			if (todo.StatusId == statusId)
				count++;
			foreach (var sub in todo.SubTasks)
			{
				if (sub.StatusId == statusId)
					count++;
			}
		}
		return count;
	}

	public async Task<bool> DeleteAsync(Status status)
	{
		if (status is null)
			return false;

		if (BuiltInStatusIds.IsBuiltIn(status.Id))
		{
			Logger.LogWarning("Refusing to delete built-in status {Id}", status.Id);
			return false;
		}

		var deletedId = status.Id;
		var now = DateTime.Now;

		await Repository.Delete(status);

		var todos = await _todoRepository.GetTodos();
		foreach (var todo in todos)
		{
			var changed = false;

			if (todo.StatusId == deletedId)
			{
				todo.StatusId = BuiltInStatusIds.None;
				changed = true;
			}

			foreach (var sub in todo.SubTasks)
			{
				if (sub.StatusId == deletedId)
				{
					sub.StatusId = BuiltInStatusIds.None;
					changed = true;
				}
			}

			if (changed)
			{
				todo.UpdatedAt = now;
				await _todoRepository.AddOrUpdate(todo);
			}
		}

		_items.RemoveAll(s => s.Id == deletedId);
		NotifyChanged();
		return true;
	}
}
