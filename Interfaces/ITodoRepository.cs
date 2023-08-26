using TodoList.Models;

namespace TodoList.Data;
public interface ITodoRepository
{
	bool Add(TodoItem todo);
	void Delete(TodoItem todo);

	IQueryable<TodoItem> GetTodos();
}