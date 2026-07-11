using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

public class EfProjectRepository : EfRepositoryBase, IProjectRepository, IRepository<Project>
{
	public EfProjectRepository(IDbContextFactory<AppDbContext> dbFactory, ICurrentUserContext user)
		: base(dbFactory, user) { }

	public Task InitializeAsync() => Task.CompletedTask;

	public Task<Project?> Get(Guid id) => GetById(id);

	public async Task<List<Project>> GetAll() => await GetProjects();

	public async Task ClearAll()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		await db.Projects.Where(p => p.UserId == userId).ExecuteDeleteAsync();
	}

	public async Task<bool> AddOrUpdate(Project project)
	{
		if (project is null || !project.IsValid() || !PassesDataAnnotations(project)) return false;
		var userId = RequireUserId();
		project.UserId = userId;

		await using var db = await CreateDbAsync();
		var existing = await db.Projects.FirstOrDefaultAsync(p => p.Id == project.Id && p.UserId == userId);
		if (existing is null)
		{
			project.UpdatedAt ??= DateTime.UtcNow;
			db.Projects.Add(project);
		}
		else
		{
			project.UpdatedAt = DateTime.UtcNow;
			db.Entry(existing).CurrentValues.SetValues(project);
		}
		await db.SaveChangesAsync();
		return true;
	}

	public async Task Delete(Project project)
	{
		if (project is null) return;
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		await db.Projects.Where(p => p.Id == project.Id && p.UserId == userId).ExecuteDeleteAsync();
	}

	public async Task<List<Project>> GetProjects()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await db.Projects
			.AsNoTracking()
			.Where(p => p.UserId == userId)
			.OrderBy(p => p.CreatedAt)
			.ToListAsync();
	}

	public async Task<Project?> GetById(Guid id)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await db.Projects
			.AsNoTracking()
			.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
	}
}
