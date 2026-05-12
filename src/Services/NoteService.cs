using TodoList.Data;
using TodoList.Models;

namespace TodoList.Services;

public class NoteService : EntityServiceBase<ProjectNote>, INoteService
{
	private readonly INoteRepository _noteRepository;

	public event Action? OnNotesChanged
	{
		add => OnChanged += value;
		remove => OnChanged -= value;
	}

	public IReadOnlyList<ProjectNote> Notes => Items;

	public NoteService(INoteRepository repository, ILogger<NoteService> logger)
		: base((IRepository<ProjectNote>)repository, logger)
	{
		_noteRepository = repository;
	}

	public override async Task InitializeAsync()
	{
		await Repository.InitializeAsync();
		_items = (await Repository.GetAll()).OrderByDescending(n => n.CreatedAt).ToList();
		NotifyChanged();
	}

	public async Task<bool> SaveNoteAsync(ProjectNote note)
	{
		note.UpdatedAt = DateTime.Now;
		var success = await Repository.AddOrUpdate(note);
		if (success)
		{
			var idx = _items.FindIndex(n => n.Id == note.Id);
			if (idx >= 0)
				_items[idx] = note;
			else
				_items.Insert(0, note);
			NotifyChanged();
		}
		return success;
	}

	public async Task DeleteNoteAsync(ProjectNote note)
	{
		await Repository.Delete(note);
		_items.RemoveAll(n => n.Id == note.Id);
		NotifyChanged();
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
	}
}
