using TodoList.Models;

namespace TodoList.Services;

public interface INoteService
{
	event Action? OnNotesChanged;
	IReadOnlyList<ProjectNote> Notes { get; }
	Task InitializeAsync();
	Task<bool> SaveNoteAsync(ProjectNote note);
	Task DeleteNoteAsync(ProjectNote note);
	IReadOnlyList<ProjectNote> GetNotesForProject(Guid projectId);
	Task DeleteNotesByProjectAsync(Guid projectId);
}
