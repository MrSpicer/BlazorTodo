using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;
using TodoList.Models.Enums;

namespace TodoList.Data.Repositories;

// Reference data (statuses) is stored per owner and shared read-only by all members. Reads resolve
// by owner so shared todos render their labels. Editing/removing another owner's shared rows is
// gated by ReferenceModify/ReferenceRemove; a user's own rows are always writable. New rows are
// created in the acting user's own partition.
public class EfStatusRepository : EfRepositoryBase, IStatusRepository, IRepository<Status>
{
	public EfStatusRepository(IDbContextFactory<AppDbContext> dbFactory, ICurrentUserContext user)
		: base(dbFactory, user) { }

	public Task InitializeAsync() => Task.CompletedTask;

	public Task<List<Status>> GetAll() => GetStatuses();

	public async Task<bool> AddOrUpdate(Status status)
	{
		if (status is null || !status.IsValid() || !PassesDataAnnotations(status)) return false;
		var userId = RequireUserId();

		await using var db = await CreateDbAsync();
		var ownerIds = ProjectAccessQueries.AccessibleOwnerIds(db, userId);
		var existing = await db.Statuses.FirstOrDefaultAsync(s =>
			s.Id == status.Id && (s.UserId == userId || ownerIds.Contains(s.UserId)));
		if (existing is null)
		{
			status.UserId = userId;
			status.UpdatedAt ??= DateTime.UtcNow;
			db.Statuses.Add(status);
		}
		else
		{
			if (existing.UserId != userId &&
				!await ProjectAccessQueries.OwnerIdsWhereMemberHas(db, userId, ProjectPermission.ReferenceModify)
					.ContainsAsync(existing.UserId))
				return false;
			status.UserId = existing.UserId;
			status.UpdatedAt = DateTime.UtcNow;
			db.Entry(existing).CurrentValues.SetValues(status);
		}
		await db.SaveChangesAsync();
		return true;
	}

	public async Task Delete(Status status)
	{
		if (status is null) return;
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		var ownerIds = ProjectAccessQueries.AccessibleOwnerIds(db, userId);
		var existing = await db.Statuses.FirstOrDefaultAsync(s =>
			s.Id == status.Id && (s.UserId == userId || ownerIds.Contains(s.UserId)));
		if (existing is null) return;
		if (existing.UserId != userId &&
			!await ProjectAccessQueries.OwnerIdsWhereMemberHas(db, userId, ProjectPermission.ReferenceRemove)
				.ContainsAsync(existing.UserId))
			return;
		await db.Statuses.Where(s => s.Id == existing.Id).ExecuteDeleteAsync();
	}

	public async Task ClearAll()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		await db.Statuses.Where(s => s.UserId == userId).ExecuteDeleteAsync();
	}

	public async Task<List<Status>> GetStatuses()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		// Resolve-by-owner: include custom statuses owned by any accessible project's owner.
		var ownerIds = ProjectAccessQueries.AccessibleOwnerIds(db, userId);
		return await db.Statuses
			.AsNoTracking()
			.Where(s => s.UserId == userId || ownerIds.Contains(s.UserId))
			.ToListAsync();
	}

	public async Task<Status?> Get(Guid id)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		// Resolve-by-owner: a member may fetch a shared status owned by an accessible project's owner.
		var ownerIds = ProjectAccessQueries.AccessibleOwnerIds(db, userId);
		return await db.Statuses
			.AsNoTracking()
			.FirstOrDefaultAsync(s => s.Id == id && (s.UserId == userId || ownerIds.Contains(s.UserId)));
	}
}
