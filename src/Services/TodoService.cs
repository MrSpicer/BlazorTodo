using TodoList.Data;
using TodoList.Identity;
using TodoList.Models;
using TodoList.Models.Enums;
using TodoList.Realtime;
using TodoList.Services.Access;

namespace TodoList.Services;

public class TodoService : EntityServiceBase<TodoItem>, ITodoService
{
	private readonly ITodoRepository _todoRepository;
	private readonly IChangeLogFormatter _formatter;
	private readonly IStatusService _statusService;
	private readonly IPriorityService _priorityService;
	private readonly IUserChangeBus _bus;
	private readonly ICurrentUserContext _user;
	private readonly IProjectAccessResolver _access;

	public event Action? OnTodosChanged
	{
		add => OnChanged += value;
		remove => OnChanged -= value;
	}

	public IReadOnlyList<TodoItem> Todos => Items;

	public TodoService(
		ITodoRepository repository,
		IChangeLogFormatter formatter,
		IStatusService statusService,
		IPriorityService priorityService,
		IUserChangeBus bus,
		ICurrentUserContext user,
		IProjectAccessResolver access,
		ILogger<TodoService> logger)
		: base((IRepository<TodoItem>)repository, logger)
	{
		_todoRepository = repository;
		_formatter = formatter;
		_statusService = statusService;
		_priorityService = priorityService;
		_bus = bus;
		_user = user;
		_access = access;
	}

	public async Task RefreshAsync()
	{
		_items = await Repository.GetAll();
		NotifyChanged();
	}

	// When the change is scoped to a project, fan out to that project's audience (owner + accepted
	// members). For cross-project operations (clear-all) there is no single project, so notify the
	// acting user only.
	private async Task PublishChange(Guid? projectId)
	{
		if (!_user.IsAuthenticated) return;
		if (projectId is Guid pid)
		{
			foreach (var userId in await _access.AudienceUserIdsAsync(pid))
				await _bus.PublishAsync(new UserChangeEvent(userId, ChangeKind.Todos));
		}
		else
		{
			await _bus.PublishAsync(new UserChangeEvent(_user.UserId, ChangeKind.Todos));
		}
	}

	public override async Task InitializeAsync()
	{
		await Repository.InitializeAsync();
		_items = await Repository.GetAll();
		await MigrateStatusIdsAsync();
		await MigratePriorityIdsAsync();
		await FlattenLegacySubTasksAsync();
		await ResetStaleTodosAsync();
		NotifyChanged();
	}

	// One-time migration of pre-v1.5 nested-children data. Legacy blobs stored
	// children inside parent.SubTasks; we hoist each child to a top-level _items
	// entry with ParentId set, then clear the parent's list. Idempotent — once
	// SubTasks is empty everywhere, this is a no-op.
	private async Task FlattenLegacySubTasksAsync()
	{
		var toFlatten = _items.Where(t => t.SubTasks.Count > 0).ToList();
		if (toFlatten.Count == 0)
			return;

		foreach (var parent in toFlatten)
		{
			foreach (var sub in parent.SubTasks)
			{
				sub.ParentId = parent.Id;
				sub.ProjectId = parent.ProjectId;
				await Repository.AddOrUpdate(sub);
			}
			parent.SubTasks.Clear();
			await Repository.AddOrUpdate(parent);
		}

		_items = await Repository.GetAll();
	}

	private async Task MigrateStatusIdsAsync()
	{
		foreach (var todo in _items)
		{
			var changed = TryFillStatusId(todo);
			foreach (var sub in todo.SubTasks)
				changed |= TryFillStatusId(sub);
			if (changed)
				await Repository.AddOrUpdate(todo);
		}

		static bool TryFillStatusId(TodoItem t)
		{
			if (t.StatusId != Guid.Empty)
				return false;
			t.StatusId = BuiltInStatusIds.FromLegacyEnum((int)t.Status);
			return true;
		}
	}

	private async Task MigratePriorityIdsAsync()
	{
		foreach (var todo in _items)
		{
			var changed = TryFillPriorityId(todo);
			foreach (var sub in todo.SubTasks)
				changed |= TryFillPriorityId(sub);
			if (changed)
				await Repository.AddOrUpdate(todo);
		}

		static bool TryFillPriorityId(TodoItem t)
		{
			if (t.PriorityId != Guid.Empty)
				return false;
			t.PriorityId = BuiltInPriorityIds.FromLegacyEnum((int)t.Priority);
			return true;
		}
	}

	private async Task ResetStaleTodosAsync()
	{
		var cutoff = DateTime.UtcNow.AddDays(-7);

		foreach (var todo in _items)
		{
			if (todo.StatusId == BuiltInStatusIds.New && todo.CreatedAt < cutoff)
			{
				todo.StatusId = BuiltInStatusIds.None;
				await Repository.AddOrUpdate(todo);
			}
		}
	}

	public async Task<bool> SaveTodoAsync(TodoItem todo)
	{
		var now = DateTime.UtcNow;
		var existed = _items.Any(t => t.Id == todo.Id);

		if ((todo.StatusId == Guid.Empty || todo.StatusId == BuiltInStatusIds.None) && !existed)
			todo.StatusId = BuiltInStatusIds.New;

		if (todo.PriorityId == Guid.Empty)
			todo.PriorityId = BuiltInPriorityIds.FromLegacyEnum((int)todo.Priority);

		if (!existed)
		{
			// Brand-new todo (UpdatedAt unset and no prior history) → record creation.
			// Imported todos already have their own UpdatedAt/ChangeLog — preserve them.
			if (todo.UpdatedAt is null && todo.ChangeLog.Count == 0)
			{
				todo.UpdatedAt = now;
				todo.ChangeLog.Add(new TodoChangeLogEntry
				{
					ChangedAt = now,
					Field = "Created",
					OldValue = null,
					NewValue = todo.Title
				});
			}
		}
		else
		{
			var old = await Repository.Get(todo.Id);
			if (old != null)
			{
				var entries = BuildChangeEntries(old, todo, now).ToList();
				if (entries.Count > 0)
				{
					todo.ChangeLog.AddRange(entries);
					todo.UpdatedAt = now;
				}
			}
		}

		var success = await Repository.AddOrUpdate(todo);
		if (success)
		{
			var idx = _items.FindIndex(t => t.Id == todo.Id);
			if (idx >= 0)
				_items[idx] = todo;
			else
				_items.Add(todo);
			NotifyChanged();
			await PublishChange(todo.ProjectId);
		}
		return success;
	}

	public async Task DeleteTodoAsync(TodoItem todo)
	{
		// Cascade: delete children before the parent so a partial failure leaves
		// no orphan rows pointing at a deleted parent.
		var children = _items.Where(t => t.ParentId == todo.Id).ToList();
		foreach (var c in children)
			await Repository.Delete(c);
		await Repository.Delete(todo);
		_items.RemoveAll(t => t.Id == todo.Id || t.ParentId == todo.Id);
		NotifyChanged();
		await PublishChange(todo.ProjectId);
	}

	public async Task UpdateStatusAsync(TodoItem todo, Guid newStatusId)
	{
		var oldStatusId = todo.StatusId;
		todo.StatusId = newStatusId;

		if (newStatusId == BuiltInStatusIds.Done && !todo.CompletedAt.HasValue)
			todo.CompletedAt = DateTime.UtcNow;

		if (newStatusId == BuiltInStatusIds.InProgress && !todo.StartedAt.HasValue)
			todo.StartedAt = DateTime.UtcNow;

		if (oldStatusId != newStatusId)
		{
			var now = DateTime.UtcNow;
			todo.ChangeLog.Add(new TodoChangeLogEntry
			{
				ChangedAt = now,
				Field = "Status",
				OldValue = StatusName(oldStatusId),
				NewValue = StatusName(newStatusId)
			});
			todo.UpdatedAt = now;
		}

		await Repository.AddOrUpdate(todo);
		NotifyChanged();
		await PublishChange(todo.ProjectId);
	}

	private string StatusName(Guid id) => _formatter.StatusName(id);

	public async Task ClearAllAsync()
	{
		await Repository.ClearAll();
		_items.Clear();
		NotifyChanged();
		await PublishChange(null);
	}

	public async Task ClearAllAsync(Guid? projectId = null)
	{
		if (projectId == null)
		{
			await Repository.ClearAll();
			_items.Clear();
		}
		else
		{
			await _todoRepository.DeleteByProject(projectId.Value);
			_items.RemoveAll(t => t.ProjectId == projectId.Value);
		}
		NotifyChanged();
		await PublishChange(projectId);
	}

	public async Task DeleteTodosByProjectAsync(Guid projectId)
	{
		await _todoRepository.DeleteByProject(projectId);
		_items.RemoveAll(t => t.ProjectId == projectId);
		NotifyChanged();
		await PublishChange(projectId);
	}

	public IReadOnlyList<TodoItem> GetSubTasks(Guid parentId) =>
		_items.Where(t => t.ParentId == parentId)
			.OrderBy(t => t.CreatedAt)
			.ToList();

	public IEnumerable<TodoItem> GetFilteredAndSorted(FilterOption filter, SortOption sort, Guid? projectId = null)
	{
		// Top-level only — subtasks render inside their parent row, not in the list itself.
		var filtered = _items.Where(t => t.ParentId is null);

		if (projectId.HasValue)
			filtered = filtered.Where(t => t.ProjectId == projectId.Value);

		filtered = filter switch
		{
			FilterOption.Active => filtered.Where(t => !t.IsDone),
			FilterOption.Completed => filtered.Where(t => t.IsDone),
			_ => filtered
		};

		return sort switch
		{
			SortOption.Priority => filtered.OrderByDescending(t => PriorityRank(t.PriorityId)),
			SortOption.Status => filtered.OrderBy(t => StatusRank(t.StatusId)),
			_ => filtered.OrderByDescending(t => t.CreatedAt)
		};
	}

	public IEnumerable<TodoItem> GetFilteredAndSorted(TodoFilterCriteria criteria, Guid? projectId = null)
	{
		var filtered = _items.Where(t => t.ParentId is null);

		if (projectId.HasValue)
			filtered = filtered.Where(t => t.ProjectId == projectId.Value);

		if (!string.IsNullOrWhiteSpace(criteria.SearchText))
		{
			var searchLower = criteria.SearchText.ToLowerInvariant();
			filtered = filtered.Where(t =>
				t.Title.ToLowerInvariant().Contains(searchLower) ||
				t.Description.ToLowerInvariant().Contains(searchLower));
		}

		if (criteria.SelectedPriorities.Any())
			filtered = filtered.Where(t => criteria.SelectedPriorities.Contains(t.PriorityId));

		if (criteria.SelectedStatuses.Any())
			filtered = filtered.Where(t => criteria.SelectedStatuses.Contains(t.StatusId));

		if (criteria.SortCriteria.Count == 0)
			return filtered;
		IOrderedEnumerable<TodoItem> sorted = ApplyFirstSort(filtered, criteria.SortCriteria[0]);
		foreach (var c in criteria.SortCriteria.Skip(1))
			sorted = ApplyThenSort(sorted, c);
		return sorted;
	}

	private IOrderedEnumerable<TodoItem> ApplyFirstSort(IEnumerable<TodoItem> source, SortCriterion c) =>
		c.Option switch
		{
			SortOption.Priority => c.Descending ? source.OrderByDescending(t => PriorityRank(t.PriorityId)) : source.OrderBy(t => PriorityRank(t.PriorityId)),
			SortOption.Status   => c.Descending ? source.OrderByDescending(t => StatusRank(t.StatusId)) : source.OrderBy(t => StatusRank(t.StatusId)),
			_                   => c.Descending ? source.OrderByDescending(t => t.CreatedAt) : source.OrderBy(t => t.CreatedAt),
		};

	private IOrderedEnumerable<TodoItem> ApplyThenSort(IOrderedEnumerable<TodoItem> source, SortCriterion c) =>
		c.Option switch
		{
			SortOption.Priority => c.Descending ? source.ThenByDescending(t => PriorityRank(t.PriorityId)) : source.ThenBy(t => PriorityRank(t.PriorityId)),
			SortOption.Status   => c.Descending ? source.ThenByDescending(t => StatusRank(t.StatusId)) : source.ThenBy(t => StatusRank(t.StatusId)),
			_                   => c.Descending ? source.ThenByDescending(t => t.CreatedAt) : source.ThenBy(t => t.CreatedAt),
		};

	private int StatusRank(Guid id)
	{
		var natural = BuiltInStatusIds.NaturalOrder(id);
		if (natural != int.MaxValue)
			return natural;
		var customs = _statusService.Statuses
			.Where(s => !s.IsBuiltIn)
			.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();
		var idx = customs.FindIndex(s => s.Id == id);
		return idx >= 0 ? 1000 + idx : int.MaxValue;
	}

	private int PriorityRank(Guid id)
	{
		var p = _priorityService.GetById(id);
		if (p is null)
			return int.MinValue;
		// Higher Rank = more important. Builtin natural order falls back via Rank.
		return p.Rank;
	}

	public int GetActiveCount(Guid? projectId = null)
	{
		var todos = _items.Where(t => t.ParentId is null);
		if (projectId.HasValue)
			todos = todos.Where(t => t.ProjectId == projectId.Value);
		return todos.Count(t => !BuiltInStatusIds.IsCompletedLike(t.StatusId));
	}

	public int GetCompletedCount(Guid? projectId = null)
	{
		var todos = _items.Where(t => t.ParentId is null);
		if (projectId.HasValue)
			todos = todos.Where(t => t.ProjectId == projectId.Value);
		return todos.Count(t => t.StatusId == BuiltInStatusIds.Done);
	}

	private IEnumerable<TodoChangeLogEntry> BuildChangeEntries(TodoItem oldItem, TodoItem newItem, DateTime now)
	{
		if (!string.Equals(oldItem.Title, newItem.Title, StringComparison.Ordinal))
			yield return Entry("Title", oldItem.Title, newItem.Title);

		if (!string.Equals(oldItem.Description, newItem.Description, StringComparison.Ordinal))
			yield return Entry("Description", oldItem.Description, newItem.Description);

		if (oldItem.PriorityId != newItem.PriorityId)
			yield return Entry("Priority", _formatter.PriorityName(oldItem.PriorityId), _formatter.PriorityName(newItem.PriorityId));

		if (oldItem.StatusId != newItem.StatusId)
			yield return Entry("Status", StatusName(oldItem.StatusId), StatusName(newItem.StatusId));

		if (oldItem.DueDate != newItem.DueDate)
			yield return Entry("DueDate", FormatDate(oldItem.DueDate), FormatDate(newItem.DueDate));

		if (oldItem.EstimatedMinutes != newItem.EstimatedMinutes)
			yield return Entry("EstimatedMinutes", oldItem.EstimatedMinutes?.ToString(), newItem.EstimatedMinutes?.ToString());

		var oldSet = oldItem.TagIds?.ToHashSet() ?? new HashSet<Guid>();
		var newSet = newItem.TagIds?.ToHashSet() ?? new HashSet<Guid>();
		if (!oldSet.SetEquals(newSet))
			yield return Entry("TagIds", FormatTags(oldItem.TagIds), FormatTags(newItem.TagIds));

		TodoChangeLogEntry Entry(string field, string? oldVal, string? newVal) => new()
		{
			ChangedAt = now,
			Field = field,
			OldValue = oldVal,
			NewValue = newVal
		};
	}

	private string FormatTags(List<Guid>? ids) => _formatter.FormatTags(ids);

	private static string FormatDate(DateTime? d) => d.HasValue ? d.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : string.Empty;
}
