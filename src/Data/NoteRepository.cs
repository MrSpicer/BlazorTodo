using Blazored.LocalStorage;
using TodoList.Models;

namespace TodoList.Data;

public class NoteRepository : ProjectScopedRepository<ProjectNote>, INoteRepository
{
	protected override string StorageName => "NoteSet";

	public NoteRepository(ILogger<NoteRepository> logger, ILocalStorageService localStorage)
		: base(logger, localStorage)
	{
	}

	public async Task<List<ProjectNote>> GetNotes()
	{
		var all = await GetAll();
		return all.OrderByDescending(n => n.CreatedAt).ToList();
	}

	public async Task<List<ProjectNote>> GetNotesByProject(Guid projectId)
	{
		var notes = await GetByProject(projectId);
		return notes.OrderByDescending(n => n.CreatedAt).ToList();
	}
}
