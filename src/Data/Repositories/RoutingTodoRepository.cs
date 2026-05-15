using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

public class RoutingTodoRepository : ITodoRepository, IRepository<TodoItem>
{
	private readonly TodoRepository _local;
	private readonly EfTodoRepository _server;
	private readonly ICurrentUserContext _user;

	public RoutingTodoRepository(TodoRepository local, EfTodoRepository server, ICurrentUserContext user)
	{
		_local = local;
		_server = server;
		_user = user;
	}

	private ITodoRepository Active => _user.IsAuthenticated ? _server : (ITodoRepository)_local;
	private IRepository<TodoItem> ActiveGeneric => _user.IsAuthenticated ? _server : _local;

	public Task<bool> AddOrUpdate(TodoItem todo) => Active.AddOrUpdate(todo);
	public Task Delete(TodoItem todo) => Active.Delete(todo);
	public Task PersistToStorage() => Active.PersistToStorage();
	public Task InitializeAsync() => Active.InitializeAsync();
	public Task ClearAll() => Active.ClearAll();
	public Task<List<TodoItem>> GetTodos() => Active.GetTodos();
	public Task<List<TodoItem>> GetTodosByProject(Guid projectId) => Active.GetTodosByProject(projectId);
	public Task<TodoItem?> Get(Guid id) => Active.Get(id);
	public Task DeleteByProject(Guid projectId) => Active.DeleteByProject(projectId);

	Task<List<TodoItem>> IRepository<TodoItem>.GetAll() => ActiveGeneric.GetAll();
}
