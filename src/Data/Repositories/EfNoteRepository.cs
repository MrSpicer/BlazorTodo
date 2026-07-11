using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

public class EfNoteRepository : EfRepositoryBase, INoteRepository, IRepository<ProjectNote>
{
	public EfNoteRepository(IDbContextFactory<AppDbContext> dbFactory, ICurrentUserContext user)
		: base(dbFactory, user) { }

	public Task InitializeAsync() => Task.CompletedTask;

	public async Task<ProjectNote?> Get(Guid id)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await db.Notes
			.AsNoTracking()
			.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
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
		if (note is null || !note.IsValid()) return false;
		var userId = RequireUserId();
		note.UserId = userId;

		await using var db = await CreateDbAsync();

		// Ownership guard: the target project must belong to the caller — a note may not be
		// attached to another user's project GUID.
		if (!await db.Projects.AnyAsync(p => p.Id == note.ProjectId && p.UserId == userId))
			return false;

		var existing = await db.Notes.FirstOrDefaultAsync(n => n.Id == note.Id && n.UserId == userId);
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
		await db.Notes.Where(n => n.Id == note.Id && n.UserId == userId).ExecuteDeleteAsync();
	}

	public async Task<List<ProjectNote>> GetNotes()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await db.Notes
			.AsNoTracking()
			.Where(n => n.UserId == userId)
			.ToListAsync();
	}

	public async Task<List<ProjectNote>> GetNotesByProject(Guid projectId)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await db.Notes
			.AsNoTracking()
			.Where(n => n.UserId == userId && n.ProjectId == projectId)
			.ToListAsync();
	}

	public async Task DeleteByProject(Guid projectId)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		await db.Notes.Where(n => n.UserId == userId && n.ProjectId == projectId).ExecuteDeleteAsync();
	}
}
