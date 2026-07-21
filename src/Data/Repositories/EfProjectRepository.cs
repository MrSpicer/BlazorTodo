using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;
using TodoList.Models.Enums;

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

		await using var db = await CreateDbAsync();
		var existing = await db.Projects.FirstOrDefaultAsync(p => p.Id == project.Id);
		if (existing is null)
		{
			// New project: the creator becomes the owner.
			project.UserId = userId;
			project.UpdatedAt ??= DateTime.UtcNow;
			db.Projects.Add(project);
		}
		else
		{
			// Editing project properties (name/description/colour/default) requires ProjectModify;
			// the owner has it implicitly. Ownership itself can never be reassigned here.
			if (!await ProjectAccessQueries.HasPermissionAsync(db, userId, existing.Id, ProjectPermission.ProjectModify))
				return false;
			project.UserId = existing.UserId;
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
		// Deleting a project requires ProjectRemove; the owner has it implicitly.
		if (!await ProjectAccessQueries.HasPermissionAsync(db, userId, project.Id, ProjectPermission.ProjectRemove)) return;
		await db.Projects.Where(p => p.Id == project.Id).ExecuteDeleteAsync();
	}

	public async Task<List<Project>> GetProjects()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await ProjectAccessQueries.AccessibleProjects(db, userId)
			.AsNoTracking()
			.OrderBy(p => p.CreatedAt)
			.ToListAsync();
	}

	public async Task<Project?> GetById(Guid id)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await ProjectAccessQueries.AccessibleProjects(db, userId)
			.AsNoTracking()
			.FirstOrDefaultAsync(p => p.Id == id);
	}
}
