using TodoList.Data;
using TodoList.Models;

namespace TodoList.Services;

public class NoteService : INoteService
{
	private readonly INoteRepository _repository;
	private List<ProjectNote> _notes = new();

	public event Action? OnNotesChanged;
	public IReadOnlyList<ProjectNote> Notes => _notes.AsReadOnly();

	public NoteService(INoteRepository repository)
	{
		_repository = repository;
	}

	public async Task InitializeAsync()
	{
		await _repository.InitializeAsync();
		_notes = await _repository.GetNotes();
		NotifyStateChanged();
	}

	public async Task<bool> SaveNoteAsync(ProjectNote note)
	{
		var success = await _repository.AddOrUpdate(note);
		if (success)
		{
			_notes = await _repository.GetNotes();
			NotifyStateChanged();
		}
		return success;
	}

	public async Task DeleteNoteAsync(ProjectNote note)
	{
		await _repository.Delete(note);
		_notes = await _repository.GetNotes();
		NotifyStateChanged();
	}

	public IReadOnlyList<ProjectNote> GetNotesForProject(Guid projectId)
	{
		return _notes.Where(n => n.ProjectId == projectId).ToList().AsReadOnly();
	}

	public async Task DeleteNotesByProjectAsync(Guid projectId)
	{
		await _repository.DeleteByProject(projectId);
		_notes = await _repository.GetNotes();
		NotifyStateChanged();
	}

	private void NotifyStateChanged() => OnNotesChanged?.Invoke();
}
