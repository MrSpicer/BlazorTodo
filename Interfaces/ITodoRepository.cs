using TodoList.Models;

namespace TodoList.Data;
public interface ITodoRepository
{
	bool AddOrUpdate(TodoItem todo);
	void Delete(TodoItem todo);
	void PersistToStorage();
	Task InitializeAsync();

	IQueryable<TodoItem> GetTodos();
}