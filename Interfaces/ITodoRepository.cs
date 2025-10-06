using TodoList.Models;

namespace TodoList.Data;
public interface ITodoRepository
{
	Task<bool> AddOrUpdate(TodoItem todo);
	Task Delete(TodoItem todo);
	Task PersistToStorage();
	Task InitializeAsync();
	Task ClearAll();

	Task<List<TodoItem>> GetTodos();
}