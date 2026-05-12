using Blazored.LocalStorage;
using TodoList.Models;

namespace TodoList.Data;

public class TodoRepository : ProjectScopedRepository<TodoItem>, ITodoRepository
{
	protected override string StorageName => "TodoSet";

	public TodoRepository(ILogger<TodoRepository> logger, ILocalStorageService localStorage)
		: base(logger, localStorage)
	{
	}

	Task<bool> ITodoRepository.AddOrUpdate(TodoItem todo) => AddOrUpdate(todo);
	Task ITodoRepository.Delete(TodoItem todo) => Delete(todo);
	Task ITodoRepository.InitializeAsync() => InitializeAsync();
	Task ITodoRepository.ClearAll() => ClearAll();
	Task ITodoRepository.PersistToStorage() => PersistToStorage();
	Task<List<TodoItem>> ITodoRepository.GetTodos() => GetAll();
	Task<List<TodoItem>> ITodoRepository.GetTodosByProject(Guid projectId) => GetByProject(projectId);
	Task<TodoItem?> ITodoRepository.Get(Guid id) => Get(id);
	Task ITodoRepository.DeleteByProject(Guid projectId) => DeleteByProject(projectId);
}
