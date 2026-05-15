using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

public class RoutingTagRepository : ITagRepository, IRepository<Tag>
{
	private readonly TagRepository _local;
	private readonly EfTagRepository _server;
	private readonly ICurrentUserContext _user;

	public RoutingTagRepository(TagRepository local, EfTagRepository server, ICurrentUserContext user)
	{
		_local = local;
		_server = server;
		_user = user;
	}

	private ITagRepository Active => _user.IsAuthenticated ? _server : (ITagRepository)_local;
	private IRepository<Tag> ActiveGeneric => _user.IsAuthenticated ? _server : _local;

	public Task<bool> AddOrUpdate(Tag tag) => Active.AddOrUpdate(tag);
	public Task Delete(Tag tag) => Active.Delete(tag);
	public Task InitializeAsync() => Active.InitializeAsync();
	public Task ClearAll() => Active.ClearAll();
	public Task<List<Tag>> GetTags() => Active.GetTags();
	public Task<Tag?> Get(Guid id) => Active.Get(id);

	Task<List<Tag>> IRepository<Tag>.GetAll() => ActiveGeneric.GetAll();
}
