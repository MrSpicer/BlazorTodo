using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

public class RoutingNoteRepository : INoteRepository, IRepository<ProjectNote>
{
	private readonly NoteRepository _local;
	private readonly EfNoteRepository _server;
	private readonly ICurrentUserContext _user;

	public RoutingNoteRepository(NoteRepository local, EfNoteRepository server, ICurrentUserContext user)
	{
		_local = local;
		_server = server;
		_user = user;
	}

	private INoteRepository Active => _user.IsAuthenticated ? _server : (INoteRepository)_local;
	private IRepository<ProjectNote> ActiveGeneric => _user.IsAuthenticated ? _server : _local;

	public Task InitializeAsync() => Active.InitializeAsync();
	public Task<bool> AddOrUpdate(ProjectNote note) => Active.AddOrUpdate(note);
	public Task Delete(ProjectNote note) => Active.Delete(note);
	public Task<List<ProjectNote>> GetNotes() => Active.GetNotes();
	public Task<List<ProjectNote>> GetNotesByProject(Guid projectId) => Active.GetNotesByProject(projectId);
	public Task DeleteByProject(Guid projectId) => Active.DeleteByProject(projectId);

	Task<ProjectNote?> IRepository<ProjectNote>.Get(Guid id) => ActiveGeneric.Get(id);
	Task<List<ProjectNote>> IRepository<ProjectNote>.GetAll() => ActiveGeneric.GetAll();
	Task IRepository<ProjectNote>.ClearAll() => ActiveGeneric.ClearAll();
}
