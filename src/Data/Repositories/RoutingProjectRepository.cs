using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

public class RoutingProjectRepository : IProjectRepository, IRepository<Project>
{
	private readonly ProjectRepository _local;
	private readonly EfProjectRepository _server;
	private readonly ICurrentUserContext _user;

	public RoutingProjectRepository(ProjectRepository local, EfProjectRepository server, ICurrentUserContext user)
	{
		_local = local;
		_server = server;
		_user = user;
	}

	private IProjectRepository Active => _user.IsAuthenticated ? _server : _local;
	private IRepository<Project> ActiveGeneric => _user.IsAuthenticated ? _server : _local;

	public Task InitializeAsync() => Active.InitializeAsync();
	public Task<bool> AddOrUpdate(Project project) => Active.AddOrUpdate(project);
	public Task Delete(Project project) => Active.Delete(project);
	public Task<List<Project>> GetProjects() => Active.GetProjects();
	public Task<Project?> GetById(Guid id) => Active.GetById(id);

	Task<Project?> IRepository<Project>.Get(Guid id) => ActiveGeneric.Get(id);
	Task<List<Project>> IRepository<Project>.GetAll() => ActiveGeneric.GetAll();
	Task IRepository<Project>.ClearAll() => ActiveGeneric.ClearAll();
}
