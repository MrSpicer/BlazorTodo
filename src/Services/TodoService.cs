using TodoList.Data;
using TodoList.Models;
using TodoList.Models.Enums;

namespace TodoList.Services;

/// <summary>
/// Implementation of ITodoService for managing todo items.
/// </summary>
public class TodoService : ITodoService
{
	private readonly ITodoRepository _repository;
	private readonly ITagService _tagService;
	private readonly IStatusService _statusService;
	private List<TodoItem> _todos = new();

	public event Action? OnTodosChanged;
	public IReadOnlyList<TodoItem> Todos => _todos.AsReadOnly();

	public TodoService(ITodoRepository repository, ITagService tagService, IStatusService statusService)
	{
		_repository = repository;
		_tagService = tagService;
		_statusService = statusService;
	}

	public async Task InitializeAsync()
	{
		await _repository.InitializeAsync();
		_todos = await _repository.GetTodos();
		await MigrateStatusIdsAsync();
		await ResetStaleTodosAsync();
		NotifyStateChanged();
	}

	private async Task MigrateStatusIdsAsync()
	{
		foreach (var todo in _todos)
		{
			var changed = TryFillStatusId(todo);
			foreach (var sub in todo.SubTasks)
				changed |= TryFillStatusId(sub);
			if (changed)
				await _repository.AddOrUpdate(todo);
		}

		static bool TryFillStatusId(TodoItem t)
		{
			if (t.StatusId != Guid.Empty)
				return false;
			t.StatusId = BuiltInStatusIds.FromLegacyEnum((int)t.Status);
			return true;
		}
	}

	private async Task ResetStaleTodosAsync()
	{
		var cutoff = DateTime.Now.AddDays(-7);

		foreach (var todo in _todos)
		{
			var dirty = false;

			if (todo.StatusId == BuiltInStatusIds.New && todo.CreatedAt < cutoff)
			{
				todo.StatusId = BuiltInStatusIds.None;
				dirty = true;
			}

			foreach (var sub in todo.SubTasks)
			{
				if (sub.StatusId == BuiltInStatusIds.New && sub.CreatedAt < cutoff)
				{
					sub.StatusId = BuiltInStatusIds.None;
					dirty = true;
				}
			}

			if (dirty)
				await _repository.AddOrUpdate(todo);
		}
	}

	public async Task<bool> SaveTodoAsync(TodoItem todo)
	{
		var now = DateTime.Now;
		var existed = _todos.Any(t => t.Id == todo.Id);

		if ((todo.StatusId == Guid.Empty || todo.StatusId == BuiltInStatusIds.None) && !existed)
			todo.StatusId = BuiltInStatusIds.New;

		if (!existed)
		{
			// Brand-new todo (UpdatedAt unset and no prior history) → record creation.
			// Imported todos already have their own UpdatedAt/ChangeLog/LastSyncedAt — preserve them.
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
			var old = await _repository.Get(todo.Id);
			if (old != null)
			{
				var entries = BuildChangeEntries(old, todo, now).ToList();
				if (entries.Count > 0)
				{
					todo.ChangeLog.AddRange(entries);
					todo.UpdatedAt = now;
				}

				var oldSubsById = old.SubTasks.ToDictionary(s => s.Id);
				foreach (var sub in todo.SubTasks)
				{
					if (oldSubsById.TryGetValue(sub.Id, out var oldSub))
					{
						var subEntries = BuildChangeEntries(oldSub, sub, now).ToList();
						if (subEntries.Count > 0)
						{
							sub.ChangeLog.AddRange(subEntries);
							sub.UpdatedAt = now;
						}
					}
					else
					{
						sub.UpdatedAt = now;
						if (!sub.ChangeLog.Any(e => e.Field == "Created"))
						{
							sub.ChangeLog.Add(new TodoChangeLogEntry
							{
								ChangedAt = now,
								Field = "Created",
								OldValue = null,
								NewValue = sub.Title
							});
						}
					}
				}
			}
		}

		var success = await _repository.AddOrUpdate(todo);
		if (success)
		{
			_todos = await _repository.GetTodos();
			NotifyStateChanged();
		}
		return success;
	}

	public async Task DeleteTodoAsync(TodoItem todo)
	{
		await _repository.Delete(todo);
		_todos = await _repository.GetTodos();
		NotifyStateChanged();
	}

	public async Task UpdateStatusAsync(TodoItem todo, Guid newStatusId)
	{
		var oldStatusId = todo.StatusId;
		todo.StatusId = newStatusId;

		if (newStatusId == BuiltInStatusIds.Done && !todo.CompletedAt.HasValue)
			todo.CompletedAt = DateTime.Now;

		if (newStatusId == BuiltInStatusIds.InProgress && !todo.StartedAt.HasValue)
			todo.StartedAt = DateTime.Now;

		if (oldStatusId != newStatusId)
		{
			var now = DateTime.Now;
			todo.ChangeLog.Add(new TodoChangeLogEntry
			{
				ChangedAt = now,
				Field = "Status",
				OldValue = StatusName(oldStatusId),
				NewValue = StatusName(newStatusId)
			});
			todo.UpdatedAt = now;
		}

		await _repository.AddOrUpdate(todo);
		NotifyStateChanged();
	}

	private string StatusName(Guid id) =>
		_statusService.GetById(id)?.Name ?? (id == Guid.Empty ? string.Empty : id.ToString());

	public async Task ClearAllAsync()
	{
		await _repository.ClearAll();
		_todos.Clear();
		NotifyStateChanged();
	}

	public async Task ClearAllAsync(Guid? projectId = null)
	{
		if (projectId == null)
		{
			await _repository.ClearAll();
			_todos.Clear();
		}
		else
		{
			var todosToDelete = _todos.Where(t => t.ProjectId == projectId).ToList();
			foreach (var todo in todosToDelete)
			{
				await _repository.Delete(todo);
			}
			_todos = await _repository.GetTodos();
		}
		NotifyStateChanged();
	}

	public async Task DeleteTodosByProjectAsync(Guid projectId)
	{
		var todosToDelete = _todos.Where(t => t.ProjectId == projectId).ToList();
		foreach (var todo in todosToDelete)
		{
			await _repository.Delete(todo);
		}
		_todos = await _repository.GetTodos();
		NotifyStateChanged();
	}

	public async Task MarkAllSyncedAsync(DateTime syncedAt)
	{
		foreach (var todo in _todos)
		{
			todo.LastSyncedAt = syncedAt;
			foreach (var sub in todo.SubTasks)
				sub.LastSyncedAt = syncedAt;
			await _repository.AddOrUpdate(todo);
		}
		NotifyStateChanged();
	}

	public IEnumerable<TodoItem> GetFilteredAndSorted(FilterOption filter, SortOption sort, Guid? projectId = null)
	{
		var filtered = _todos.AsEnumerable();

		// Filter by project if specified
		if (projectId.HasValue)
		{
			filtered = filtered.Where(t => t.ProjectId == projectId.Value);
		}

		filtered = filter switch
		{
			FilterOption.Active => filtered.Where(t => !t.IsDone),
			FilterOption.Completed => filtered.Where(t => t.IsDone),
			_ => filtered
		};

		return sort switch
		{
			SortOption.Priority => filtered.OrderByDescending(t => t.Priority),
			SortOption.Status => filtered.OrderBy(t => StatusRank(t.StatusId)),
			_ => filtered.OrderByDescending(t => t.CreatedAt)
		};
	}

	public IEnumerable<TodoItem> GetFilteredAndSorted(TodoFilterCriteria criteria, Guid? projectId = null)
	{
		var filtered = _todos.AsEnumerable();

		// Filter by project if specified
		if (projectId.HasValue)
		{
			filtered = filtered.Where(t => t.ProjectId == projectId.Value);
		}

		// Text search - case insensitive search in title and description
		if (!string.IsNullOrWhiteSpace(criteria.SearchText))
		{
			var searchLower = criteria.SearchText.ToLowerInvariant();
			filtered = filtered.Where(t =>
				t.Title.ToLowerInvariant().Contains(searchLower) ||
				t.Description.ToLowerInvariant().Contains(searchLower));
		}

		// Filter by selected priorities
		if (criteria.SelectedPriorities.Any())
		{
			filtered = filtered.Where(t => criteria.SelectedPriorities.Contains(t.Priority));
		}

		// Filter by selected statuses
		if (criteria.SelectedStatuses.Any())
		{
			filtered = filtered.Where(t => criteria.SelectedStatuses.Contains(t.StatusId));
		}

		// Sort
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
			SortOption.Priority => c.Descending ? source.OrderByDescending(t => t.Priority) : source.OrderBy(t => t.Priority),
			SortOption.Status   => c.Descending ? source.OrderByDescending(t => StatusRank(t.StatusId)) : source.OrderBy(t => StatusRank(t.StatusId)),
			_                   => c.Descending ? source.OrderByDescending(t => t.CreatedAt) : source.OrderBy(t => t.CreatedAt),
		};

	private IOrderedEnumerable<TodoItem> ApplyThenSort(IOrderedEnumerable<TodoItem> source, SortCriterion c) =>
		c.Option switch
		{
			SortOption.Priority => c.Descending ? source.ThenByDescending(t => t.Priority) : source.ThenBy(t => t.Priority),
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

	public int GetActiveCount(Guid? projectId = null)
	{
		var todos = projectId.HasValue
			? _todos.Where(t => t.ProjectId == projectId.Value)
			: _todos;
		return todos.Count(t => !BuiltInStatusIds.IsCompletedLike(t.StatusId));
	}

	public int GetCompletedCount(Guid? projectId = null)
	{
		var todos = projectId.HasValue
			? _todos.Where(t => t.ProjectId == projectId.Value)
			: _todos;
		return todos.Count(t => t.StatusId == BuiltInStatusIds.Done);
	}

	private IEnumerable<TodoChangeLogEntry> BuildChangeEntries(TodoItem oldItem, TodoItem newItem, DateTime now)
	{
		if (!string.Equals(oldItem.Title, newItem.Title, StringComparison.Ordinal))
			yield return Entry("Title", oldItem.Title, newItem.Title);

		if (!string.Equals(oldItem.Description, newItem.Description, StringComparison.Ordinal))
			yield return Entry("Description", oldItem.Description, newItem.Description);

		if (oldItem.Priority != newItem.Priority)
			yield return Entry("Priority", oldItem.Priority.ToString(), newItem.Priority.ToString());

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

	private string FormatTags(List<Guid>? ids)
	{
		if (ids is null || ids.Count == 0)
			return string.Empty;
		var names = ids
			.Select(id => _tagService.GetById(id)?.Name ?? id.ToString("N").Substring(0, 6))
			.OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
		return string.Join(", ", names);
	}

	private static string FormatDate(DateTime? d) => d.HasValue ? d.Value.ToString("yyyy-MM-dd HH:mm") : string.Empty;

	private void NotifyStateChanged() => OnTodosChanged?.Invoke();
}
