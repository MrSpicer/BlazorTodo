using TodoList.Data;
using TodoList.Identity;
using TodoList.Models;
using TodoList.Realtime;

namespace TodoList.Services;

public class NoteService : EntityServiceBase<ProjectNote>, INoteService
{
	private readonly INoteRepository _noteRepository;
	private readonly IUserChangeBus _bus;
	private readonly ICurrentUserContext _user;

	public event Action? OnNotesChanged
	{
		add => OnChanged += value;
		remove => OnChanged -= value;
	}

	public IReadOnlyList<ProjectNote> Notes => Items;

	public NoteService(
		INoteRepository repository,
		IUserChangeBus bus,
		ICurrentUserContext user,
		ILogger<NoteService> logger)
		: base((IRepository<ProjectNote>)repository, logger)
	{
		_noteRepository = repository;
		_bus = bus;
		_user = user;
	}

	public override async Task InitializeAsync()
	{
		await Repository.InitializeAsync();
		_items = (await Repository.GetAll()).OrderByDescending(n => n.CreatedAt).ToList();
		NotifyChanged();
	}

	public async Task RefreshAsync()
	{
		_items = (await Repository.GetAll()).OrderByDescending(n => n.CreatedAt).ToList();
		NotifyChanged();
	}

	private Task PublishChange()
	{
		if (!_user.IsAuthenticated) return Task.CompletedTask;
		return _bus.PublishAsync(new UserChangeEvent(_user.UserId, ChangeKind.Notes));
	}

	public async Task<bool> SaveNoteAsync(ProjectNote note)
	{
		note.UpdatedAt = DateTime.UtcNow;
		var success = await Repository.AddOrUpdate(note);
		if (success)
		{
			var idx = _items.FindIndex(n => n.Id == note.Id);
			if (idx >= 0)
				_items[idx] = note;
			else
				_items.Insert(0, note);
			NotifyChanged();
			await PublishChange();
		}
		return success;
	}

	public async Task DeleteNoteAsync(ProjectNote note)
	{
		await Repository.Delete(note);
		_items.RemoveAll(n => n.Id == note.Id);
		NotifyChanged();
		await PublishChange();
	}

	public IReadOnlyList<ProjectNote> GetNotesForProject(Guid projectId)
	{
		return _items.Where(n => n.ProjectId == projectId).ToList().AsReadOnly();
	}

	public async Task DeleteNotesByProjectAsync(Guid projectId)
	{
		await _noteRepository.DeleteByProject(projectId);
		_items.RemoveAll(n => n.ProjectId == projectId);
		NotifyChanged();
		await PublishChange();
	}
}
