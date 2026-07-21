using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;
using TodoList.Models.Enums;

namespace TodoList.Data.Repositories;

// Reference data (tags) is stored per owner and shared read-only by all members. Reads resolve by
// owner so shared todos render their labels. Editing/removing another owner's shared rows is gated
// by ReferenceModify/ReferenceRemove; a user's own rows are always writable. New rows are created
// in the acting user's own partition.
public class EfTagRepository : EfRepositoryBase, ITagRepository, IRepository<Tag>
{
	public EfTagRepository(IDbContextFactory<AppDbContext> dbFactory, ICurrentUserContext user)
		: base(dbFactory, user) { }

	public Task InitializeAsync() => Task.CompletedTask;

	public Task<List<Tag>> GetAll() => GetTags();

	public async Task<bool> AddOrUpdate(Tag tag)
	{
		if (tag is null || !tag.IsValid() || !PassesDataAnnotations(tag)) return false;
		var userId = RequireUserId();

		await using var db = await CreateDbAsync();
		var ownerIds = ProjectAccessQueries.AccessibleOwnerIds(db, userId);
		var existing = await db.Tags.FirstOrDefaultAsync(t =>
			t.Id == tag.Id && (t.UserId == userId || ownerIds.Contains(t.UserId)));
		if (existing is null)
		{
			// New reference rows are created in the acting user's own partition.
			tag.UserId = userId;
			tag.UpdatedAt ??= DateTime.UtcNow;
			db.Tags.Add(tag);
		}
		else
		{
			if (existing.UserId != userId &&
				!await ProjectAccessQueries.OwnerIdsWhereMemberHas(db, userId, ProjectPermission.ReferenceModify)
					.ContainsAsync(existing.UserId))
				return false;
			tag.UserId = existing.UserId;
			tag.UpdatedAt = DateTime.UtcNow;
			db.Entry(existing).CurrentValues.SetValues(tag);
		}
		await db.SaveChangesAsync();
		return true;
	}

	public async Task Delete(Tag tag)
	{
		if (tag is null) return;
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		var ownerIds = ProjectAccessQueries.AccessibleOwnerIds(db, userId);
		var existing = await db.Tags.FirstOrDefaultAsync(t =>
			t.Id == tag.Id && (t.UserId == userId || ownerIds.Contains(t.UserId)));
		if (existing is null) return;
		if (existing.UserId != userId &&
			!await ProjectAccessQueries.OwnerIdsWhereMemberHas(db, userId, ProjectPermission.ReferenceRemove)
				.ContainsAsync(existing.UserId))
			return;
		await db.Tags.Where(t => t.Id == existing.Id).ExecuteDeleteAsync();
	}

	public async Task ClearAll()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		await db.Tags.Where(t => t.UserId == userId).ExecuteDeleteAsync();
	}

	public async Task<List<Tag>> GetTags()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		// Resolve-by-owner: include tags owned by any accessible project's owner so shared todos'
		// tag chips resolve. Writes (AddOrUpdate/Delete) stay scoped to the acting user.
		var ownerIds = ProjectAccessQueries.AccessibleOwnerIds(db, userId);
		return await db.Tags
			.AsNoTracking()
			.Where(t => t.UserId == userId || ownerIds.Contains(t.UserId))
			.ToListAsync();
	}

	public async Task<Tag?> Get(Guid id)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		// Resolve-by-owner: a member may fetch a shared tag owned by an accessible project's owner.
		var ownerIds = ProjectAccessQueries.AccessibleOwnerIds(db, userId);
		return await db.Tags
			.AsNoTracking()
			.FirstOrDefaultAsync(t => t.Id == id && (t.UserId == userId || ownerIds.Contains(t.UserId)));
	}
}
