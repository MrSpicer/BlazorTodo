using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

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
		tag.UserId = userId;

		await using var db = await CreateDbAsync();
		var existing = await db.Tags.FirstOrDefaultAsync(t => t.Id == tag.Id && t.UserId == userId);
		if (existing is null)
		{
			tag.UpdatedAt ??= DateTime.UtcNow;
			db.Tags.Add(tag);
		}
		else
		{
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
		await db.Tags.Where(t => t.Id == tag.Id && t.UserId == userId).ExecuteDeleteAsync();
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
		return await db.Tags
			.AsNoTracking()
			.Where(t => t.UserId == userId)
			.ToListAsync();
	}

	public async Task<Tag?> Get(Guid id)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await db.Tags
			.AsNoTracking()
			.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
	}
}
