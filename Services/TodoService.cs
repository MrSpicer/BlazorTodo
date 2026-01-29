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

    public IEnumerable<TodoItem> GetFilteredAndSorted(FilterOption filter, SortOption sort)
    {
        var filtered = filter switch
        {
            FilterOption.Active => _todos.Where(t => !t.IsDone),
            FilterOption.Completed => _todos.Where(t => t.IsDone),
            _ => _todos
        };

        return sort switch
        {
            SortOption.Priority => filtered.OrderByDescending(t => t.Priority),
            SortOption.Status => filtered.OrderBy(t => t.Status),
            _ => filtered.OrderByDescending(t => t.CreatedAt)
        };
    }

    public int GetActiveCount() => _todos.Count(t => !t.IsDone);
    
    public int GetCompletedCount() => _todos.Count(t => t.IsDone);

    private void NotifyStateChanged() => OnTodosChanged?.Invoke();
}
