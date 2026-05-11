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
			var idx = _notes.FindIndex(n => n.Id == note.Id);
			if (idx >= 0)
				_notes[idx] = note;
			else
				_notes.Insert(0, note);
			NotifyStateChanged();
		}
		return success;
	}

	public async Task DeleteNoteAsync(ProjectNote note)
	{
		await _repository.Delete(note);
		_notes.RemoveAll(n => n.Id == note.Id);
		NotifyStateChanged();
	}

	public IReadOnlyList<ProjectNote> GetNotesForProject(Guid projectId)
	{
		return _notes.Where(n => n.ProjectId == projectId).ToList().AsReadOnly();
	}

	public async Task DeleteNotesByProjectAsync(Guid projectId)
	{
		await _repository.DeleteByProject(projectId);
		_notes.RemoveAll(n => n.ProjectId == projectId);
		NotifyStateChanged();
	}

	private void NotifyStateChanged() => OnNotesChanged?.Invoke();
}
