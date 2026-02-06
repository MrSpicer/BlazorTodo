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
    private List<TodoItem> _todos = new();

    public event Action? OnTodosChanged;
    public IReadOnlyList<TodoItem> Todos => _todos.AsReadOnly();

    public TodoService(ITodoRepository repository)
    {
        _repository = repository;
    }

    public async Task InitializeAsync()
    {
        await _repository.InitializeAsync();
        _todos = await _repository.GetTodos();
        NotifyStateChanged();
    }

    public async Task<bool> SaveTodoAsync(TodoItem todo)
    {
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

    public async Task UpdateStatusAsync(TodoItem todo, TodoItemStatus newStatus)
    {
        todo.Status = newStatus;
        
        if (newStatus == TodoItemStatus.Done && !todo.CompletedAt.HasValue)
            todo.CompletedAt = DateTime.Now;
        
        if (newStatus == TodoItemStatus.InProgress && !todo.StartedAt.HasValue)
            todo.StartedAt = DateTime.Now;

        await _repository.AddOrUpdate(todo);
        NotifyStateChanged();
    }

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
			SortOption.Status => filtered.OrderBy(t => t.Status),
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
			filtered = filtered.Where(t => criteria.SelectedStatuses.Contains(t.Status));
		}

		// Sort
		return criteria.Sort switch
		{
			SortOption.Priority => filtered.OrderByDescending(t => t.Priority),
			SortOption.Status => filtered.OrderBy(t => t.Status),
			_ => filtered.OrderByDescending(t => t.CreatedAt)
		};
	}

    public int GetActiveCount(Guid? projectId = null)
    {
        var todos = projectId.HasValue 
            ? _todos.Where(t => t.ProjectId == projectId.Value) 
            : _todos;
        return todos.Count(t => !t.IsDone);
    }
    
    public int GetCompletedCount(Guid? projectId = null)
    {
        var todos = projectId.HasValue 
            ? _todos.Where(t => t.ProjectId == projectId.Value) 
            : _todos;
        return todos.Count(t => t.IsDone);
    }

    private void NotifyStateChanged() => OnTodosChanged?.Invoke();
}
