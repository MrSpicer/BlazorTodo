using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;
using TodoList.Models.Enums;

namespace TodoList.Data.Repositories;

// Notes are gated per action: reads require NotesRead, creating NotesAdd, editing NotesModify,
// deleting NotesRemove. The owner implicitly has all of them.
public class EfNoteRepository : EfRepositoryBase, INoteRepository, IRepository<ProjectNote>
{
	public EfNoteRepository(IDbContextFactory<AppDbContext> dbFactory, ICurrentUserContext user)
		: base(dbFactory, user) { }

	public Task InitializeAsync() => Task.CompletedTask;

	public async Task<ProjectNote?> Get(Guid id)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		var readableIds = ProjectAccessQueries.ProjectIdsWith(db, userId, ProjectPermission.NotesRead);
		return await db.Notes
			.AsNoTracking()
			.FirstOrDefaultAsync(n => n.Id == id && readableIds.Contains(n.ProjectId));
	}

	public Task<List<ProjectNote>> GetAll() => GetNotes();

	public async Task ClearAll()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		await db.Notes.Where(n => n.UserId == userId).ExecuteDeleteAsync();
	}

	public async Task<bool> AddOrUpdate(ProjectNote note)
	{
		if (note is null || !note.IsValid() || !PassesDataAnnotations(note)) return false;
		var userId = RequireUserId();

		await using var db = await CreateDbAsync();

		var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == note.ProjectId);
		if (project is null) return false;

		var accessibleIds = ProjectAccessQueries.AccessibleProjectIds(db, userId);
		var existing = await db.Notes.FirstOrDefaultAsync(n => n.Id == note.Id &&
			accessibleIds.Contains(n.ProjectId));

		// Object-level authorization: creating needs NotesAdd, editing needs NotesModify.
		var required = existing is null ? ProjectPermission.NotesAdd : ProjectPermission.NotesModify;
		if (!await ProjectAccessQueries.HasPermissionAsync(db, userId, note.ProjectId, required))
			return false;

		// Partition key = the project owner (matches the resolve-by-owner reference-data model).
		note.UserId = project.UserId;

		if (existing is null)
		{
			note.UpdatedAt ??= DateTime.UtcNow;
			db.Notes.Add(note);
		}
		else
		{
			note.UpdatedAt = DateTime.UtcNow;
			db.Entry(existing).CurrentValues.SetValues(note);
		}
		await db.SaveChangesAsync();
		return true;
	}

	public async Task Delete(ProjectNote note)
	{
		if (note is null) return;
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		var accessibleIds = ProjectAccessQueries.AccessibleProjectIds(db, userId);
		var existing = await db.Notes
			.FirstOrDefaultAsync(n => n.Id == note.Id && accessibleIds.Contains(n.ProjectId));
		if (existing is null) return;
		if (!await ProjectAccessQueries.HasPermissionAsync(db, userId, existing.ProjectId, ProjectPermission.NotesRemove))
			return;
		await db.Notes.Where(n => n.Id == existing.Id).ExecuteDeleteAsync();
	}

	public async Task<List<ProjectNote>> GetNotes()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		var readableIds = ProjectAccessQueries.ProjectIdsWith(db, userId, ProjectPermission.NotesRead);
		return await db.Notes
			.AsNoTracking()
			.Where(n => readableIds.Contains(n.ProjectId))
			.ToListAsync();
	}

	public async Task<List<ProjectNote>> GetNotesByProject(Guid projectId)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		var readableIds = ProjectAccessQueries.ProjectIdsWith(db, userId, ProjectPermission.NotesRead);
		return await db.Notes
			.AsNoTracking()
			.Where(n => n.ProjectId == projectId && readableIds.Contains(n.ProjectId))
			.ToListAsync();
	}

	public async Task DeleteByProject(Guid projectId)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		if (!await ProjectAccessQueries.HasPermissionAsync(db, userId, projectId, ProjectPermission.NotesRemove)) return;
		await db.Notes.Where(n => n.ProjectId == projectId).ExecuteDeleteAsync();
	}
}
