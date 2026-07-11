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

		// Built-ins live in-memory only (seeded from BuiltInStatusIds.Seed). Dedupe stored
		// rows that share a built-in Id (legacy LocalStorage may have copies) so we don't
		// show duplicates and so we don't try to re-persist built-ins to a per-user DB.
		var customStored = stored.Where(s => !BuiltInStatusIds.IsBuiltIn(s.Id)).ToList();
		_items = seeded.Concat(customStored).ToList();
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

			// Built-ins are filtered out before persistence anyway; only migrate customs.
			if (BuiltInStatusIds.IsBuiltIn(s.Id))
				continue;

			s.Color = MapLegacyColor(s.Color);
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

		// Built-in statuses are in-memory only — apply edits to the local list but skip persistence.
		if (!status.IsBuiltIn)
		{
			var ok = await Repository.AddOrUpdate(status);
			if (!ok)
				return false;
		}

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
		return todos.Count(t => t.StatusId == statusId);
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
		var now = DateTime.UtcNow;

		await Repository.Delete(status);

		var todos = await _todoRepository.GetTodos();
		foreach (var todo in todos)
		{
			if (todo.StatusId == deletedId)
			{
				todo.StatusId = BuiltInStatusIds.None;
				todo.UpdatedAt = now;
				await _todoRepository.AddOrUpdate(todo);
			}
		}

		_items.RemoveAll(s => s.Id == deletedId);
		NotifyChanged();
		return true;
	}
}
