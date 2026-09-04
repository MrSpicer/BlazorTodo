using TodoList.Models;

namespace TodoList.Services;

public interface INoteService
{
	event Action? OnNotesChanged;
	IReadOnlyList<ProjectNote> Notes { get; }
	Task InitializeAsync();
	Task RefreshAsync();
	Task<bool> SaveNoteAsync(ProjectNote note);
	Task DeleteNoteAsync(ProjectNote note);
	IReadOnlyList<ProjectNote> GetNotesForProject(Guid projectId);
	Task DeleteNotesByProjectAsync(Guid projectId);

	/// <summary>
	/// Gets a single note by id from the in-memory list, or null if not found.
	/// </summary>
	ProjectNote? GetById(Guid id);
}
