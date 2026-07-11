using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

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
		status.UserId = userId;

		await using var db = await CreateDbAsync();
		var existing = await db.Statuses.FirstOrDefaultAsync(s => s.Id == status.Id && s.UserId == userId);
		if (existing is null)
		{
			status.UpdatedAt ??= DateTime.UtcNow;
			db.Statuses.Add(status);
		}
		else
		{
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
		await db.Statuses.Where(s => s.Id == status.Id && s.UserId == userId).ExecuteDeleteAsync();
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
		return await db.Statuses
			.AsNoTracking()
			.Where(s => s.UserId == userId)
			.ToListAsync();
	}

	public async Task<Status?> Get(Guid id)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await db.Statuses
			.AsNoTracking()
			.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
	}
}
