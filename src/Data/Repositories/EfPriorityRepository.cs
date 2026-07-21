using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;
using TodoList.Models.Enums;

namespace TodoList.Data.Repositories;

// Reference data (priorities) is stored per owner and shared read-only by all members. Reads
// resolve by owner so shared todos render their labels. Editing/removing another owner's shared
// rows is gated by ReferenceModify/ReferenceRemove; a user's own rows are always writable. New
// rows are created in the acting user's own partition.
public class EfPriorityRepository : EfRepositoryBase, IPriorityRepository, IRepository<Priority>
{
	public EfPriorityRepository(IDbContextFactory<AppDbContext> dbFactory, ICurrentUserContext user)
		: base(dbFactory, user) { }

	public Task InitializeAsync() => Task.CompletedTask;

	public Task<List<Priority>> GetAll() => GetPriorities();

	public async Task<bool> AddOrUpdate(Priority priority)
	{
		if (priority is null || !priority.IsValid() || !PassesDataAnnotations(priority)) return false;
		var userId = RequireUserId();

		await using var db = await CreateDbAsync();
		var ownerIds = ProjectAccessQueries.AccessibleOwnerIds(db, userId);
		var existing = await db.Priorities.FirstOrDefaultAsync(p =>
			p.Id == priority.Id && (p.UserId == userId || ownerIds.Contains(p.UserId)));
		if (existing is null)
		{
			priority.UserId = userId;
			priority.UpdatedAt ??= DateTime.UtcNow;
			db.Priorities.Add(priority);
		}
		else
		{
			if (existing.UserId != userId &&
				!await ProjectAccessQueries.OwnerIdsWhereMemberHas(db, userId, ProjectPermission.ReferenceModify)
					.ContainsAsync(existing.UserId))
				return false;
			priority.UserId = existing.UserId;
			priority.UpdatedAt = DateTime.UtcNow;
			db.Entry(existing).CurrentValues.SetValues(priority);
		}
		await db.SaveChangesAsync();
		return true;
	}

	public async Task Delete(Priority priority)
	{
		if (priority is null) return;
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		var ownerIds = ProjectAccessQueries.AccessibleOwnerIds(db, userId);
		var existing = await db.Priorities.FirstOrDefaultAsync(p =>
			p.Id == priority.Id && (p.UserId == userId || ownerIds.Contains(p.UserId)));
		if (existing is null) return;
		if (existing.UserId != userId &&
			!await ProjectAccessQueries.OwnerIdsWhereMemberHas(db, userId, ProjectPermission.ReferenceRemove)
				.ContainsAsync(existing.UserId))
			return;
		await db.Priorities.Where(p => p.Id == existing.Id).ExecuteDeleteAsync();
	}

	public async Task ClearAll()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		await db.Priorities.Where(p => p.UserId == userId).ExecuteDeleteAsync();
	}

	public async Task<List<Priority>> GetPriorities()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		// Resolve-by-owner: include custom priorities owned by any accessible project's owner.
		var ownerIds = ProjectAccessQueries.AccessibleOwnerIds(db, userId);
		return await db.Priorities
			.AsNoTracking()
			.Where(p => p.UserId == userId || ownerIds.Contains(p.UserId))
			.OrderBy(p => p.Rank)
			.ToListAsync();
	}

	public async Task<Priority?> Get(Guid id)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		// Resolve-by-owner: a member may fetch a shared priority owned by an accessible project's owner.
		var ownerIds = ProjectAccessQueries.AccessibleOwnerIds(db, userId);
		return await db.Priorities
			.AsNoTracking()
			.FirstOrDefaultAsync(p => p.Id == id && (p.UserId == userId || ownerIds.Contains(p.UserId)));
	}
}
