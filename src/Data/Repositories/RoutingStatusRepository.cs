using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

public class RoutingStatusRepository : IStatusRepository, IRepository<Status>
{
	private readonly StatusRepository _local;
	private readonly EfStatusRepository _server;
	private readonly ICurrentUserContext _user;

	public RoutingStatusRepository(StatusRepository local, EfStatusRepository server, ICurrentUserContext user)
	{
		_local = local;
		_server = server;
		_user = user;
	}

	private IStatusRepository Active => _user.IsAuthenticated ? _server : (IStatusRepository)_local;
	private IRepository<Status> ActiveGeneric => _user.IsAuthenticated ? _server : _local;

	public Task<bool> AddOrUpdate(Status status) => Active.AddOrUpdate(status);
	public Task Delete(Status status) => Active.Delete(status);
	public Task InitializeAsync() => Active.InitializeAsync();
	public Task ClearAll() => Active.ClearAll();
	public Task<List<Status>> GetStatuses() => Active.GetStatuses();
	public Task<Status?> Get(Guid id) => Active.Get(id);

	Task<List<Status>> IRepository<Status>.GetAll() => ActiveGeneric.GetAll();
}
