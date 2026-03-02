using TodoList.Models;
using Blazored.LocalStorage;

namespace TodoList.Data;

public class NoteRepository : INoteRepository
{
	private HashSet<Guid> _noteIds = new();
	private readonly ILogger<NoteRepository> _logger;
	private readonly ILocalStorageService _localStorage;
	private const string StorageName = "NoteSet";

	public NoteRepository(ILogger<NoteRepository> logger, ILocalStorageService localStorage)
	{
		_logger = logger;
		_localStorage = localStorage;
	}

	public async Task InitializeAsync()
	{
		_noteIds = await _localStorage.GetItemAsync<HashSet<Guid>>($"{StorageName}_Ids") ?? new HashSet<Guid>();
	}

	public async Task<bool> AddOrUpdate(ProjectNote note)
	{
		if (note == null || !note.IsValid())
		{
			_logger.Log(LogLevel.Debug, "Malformed note");
			return false;
		}

		try
		{
			if (!_noteIds.Contains(note.Id))
			{
				_noteIds.Add(note.Id);
				await PersistToStorage();
			}
			await _localStorage.SetItemAsync($"{StorageName}_{note.Id}", note);
			return true;
		}
		catch (Exception ex)
		{
			_logger.Log(LogLevel.Error, $"Error updating note: {ex.Message}");
			return false;
		}
	}

	public async Task<List<ProjectNote>> GetNotes()
	{
		try
		{
			var notes = new List<ProjectNote>();
			foreach (Guid id in _noteIds)
			{
				var item = await _localStorage.GetItemAsync<ProjectNote>($"{StorageName}_{id}");
				if (item != null)
				{
					notes.Add(item);
				}
			}
			return notes.OrderByDescending(n => n.CreatedAt).ToList();
		}
		catch (Exception ex)
		{
			_logger.Log(LogLevel.Error, $"Error retrieving notes: {ex.Message}");
			return new List<ProjectNote>();
		}
	}

	public async Task<List<ProjectNote>> GetNotesByProject(Guid projectId)
	{
		var notes = await GetNotes();
		return notes.Where(n => n.ProjectId == projectId).ToList();
	}

	public async Task Delete(ProjectNote note)
	{
		if (_noteIds.Contains(note.Id))
		{
			_noteIds.Remove(note.Id);
			await PersistToStorage();
			await _localStorage.RemoveItemAsync($"{StorageName}_{note.Id}");
		}
	}

	public async Task DeleteByProject(Guid projectId)
	{
		var notes = await GetNotes();
		var toDelete = notes.Where(n => n.ProjectId == projectId).ToList();
		foreach (var note in toDelete)
		{
			_noteIds.Remove(note.Id);
			await _localStorage.RemoveItemAsync($"{StorageName}_{note.Id}");
		}
		if (toDelete.Count > 0)
		{
			await PersistToStorage();
		}
	}

	private async Task PersistToStorage()
	{
		await _localStorage.SetItemAsync($"{StorageName}_Ids", _noteIds);
	}
}
