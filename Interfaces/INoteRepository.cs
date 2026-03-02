using TodoList.Models;

namespace TodoList.Data;

public interface INoteRepository
{
	Task InitializeAsync();
	Task<bool> AddOrUpdate(ProjectNote note);
	Task Delete(ProjectNote note);
	Task<List<ProjectNote>> GetNotes();
	Task<List<ProjectNote>> GetNotesByProject(Guid projectId);
	Task DeleteByProject(Guid projectId);
}
