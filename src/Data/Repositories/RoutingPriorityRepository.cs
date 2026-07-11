using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

public class RoutingPriorityRepository : IPriorityRepository, IRepository<Priority>
{
	private readonly PriorityRepository _local;
	private readonly EfPriorityRepository _server;
	private readonly ICurrentUserContext _user;

	public RoutingPriorityRepository(PriorityRepository local, EfPriorityRepository server, ICurrentUserContext user)
	{
		_local = local;
		_server = server;
		_user = user;
	}

	private IPriorityRepository Active => _user.IsAuthenticated ? _server : (IPriorityRepository)_local;
	private IRepository<Priority> ActiveGeneric => _user.IsAuthenticated ? _server : _local;

	public Task<bool> AddOrUpdate(Priority priority) => Active.AddOrUpdate(priority);
	public Task Delete(Priority priority) => Active.Delete(priority);
	public Task InitializeAsync() => Active.InitializeAsync();
	public Task ClearAll() => Active.ClearAll();
	public Task<List<Priority>> GetPriorities() => Active.GetPriorities();
	public Task<Priority?> Get(Guid id) => Active.Get(id);

	Task<List<Priority>> IRepository<Priority>.GetAll() => ActiveGeneric.GetAll();
}
