using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

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
		priority.UserId = userId;

		await using var db = await CreateDbAsync();
		var existing = await db.Priorities.FirstOrDefaultAsync(p => p.Id == priority.Id && p.UserId == userId);
		if (existing is null)
		{
			priority.UpdatedAt ??= DateTime.UtcNow;
			db.Priorities.Add(priority);
		}
		else
		{
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
		await db.Priorities.Where(p => p.Id == priority.Id && p.UserId == userId).ExecuteDeleteAsync();
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
		return await db.Priorities
			.AsNoTracking()
			.Where(p => p.UserId == userId)
			.OrderBy(p => p.Rank)
			.ToListAsync();
	}

	public async Task<Priority?> Get(Guid id)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await db.Priorities
			.AsNoTracking()
			.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
	}
}
