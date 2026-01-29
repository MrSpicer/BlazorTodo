using TodoList.Models;
using TodoList.Models.Enums;

namespace TodoList.Services;

/// <summary>
/// Service for managing todo items with business logic.
/// </summary>
public interface ITodoService
{
    /// <summary>
    /// Event raised when the todo list changes.
    /// </summary>
    event Action? OnTodosChanged;

    /// <summary>
    /// Gets the current list of todos.
    /// </summary>
    IReadOnlyList<TodoItem> Todos { get; }

    /// <summary>
    /// Initializes the service and loads todos from storage.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Saves a new or existing todo item.
    /// </summary>
    /// <param name="todo">The todo item to save.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> SaveTodoAsync(TodoItem todo);

    /// <summary>
    /// Deletes a todo item.
    /// </summary>
    /// <param name="todo">The todo item to delete.</param>
    Task DeleteTodoAsync(TodoItem todo);

    /// <summary>
    /// Updates the status of a todo item.
    /// </summary>
    /// <param name="todo">The todo item to update.</param>
    /// <param name="newStatus">The new status.</param>
    Task UpdateStatusAsync(TodoItem todo, TodoItemStatus newStatus);

    /// <summary>
    /// Clears all todos.
    /// </summary>
    Task ClearAllAsync();

    /// <summary>
    /// Gets todos filtered and sorted by the specified options.
    /// </summary>
    /// <param name="filter">The filter option.</param>
    /// <param name="sort">The sort option.</param>
    /// <returns>Filtered and sorted todos.</returns>
    IEnumerable<TodoItem> GetFilteredAndSorted(FilterOption filter, SortOption sort);

    /// <summary>
    /// Gets the count of active (not done) todos.
    /// </summary>
    int GetActiveCount();

    /// <summary>
    /// Gets the count of completed todos.
    /// </summary>
    int GetCompletedCount();
}
