using Microsoft.EntityFrameworkCore;
using TodoList.Data;
using TodoList.Models.Enums;

namespace TodoList.Services.Access;

public class ProjectAccessResolver : IProjectAccessResolver
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;

	public ProjectAccessResolver(IDbContextFactory<AppDbContext> dbFactory)
	{
		_dbFactory = dbFactory;
	}

	public async Task<IReadOnlyList<Guid>> AccessibleProjectIdsAsync(Guid userId)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		return await ProjectAccessQueries.AccessibleProjectIds(db, userId).ToListAsync();
	}

	public async Task<bool> IsOwnerAsync(Guid userId, Guid projectId)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		return await ProjectAccessQueries.IsOwnerAsync(db, userId, projectId);
	}

	public async Task<bool> CanManageAccessAsync(Guid userId, Guid projectId)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		return await ProjectAccessQueries.CanManageAccessAsync(db, userId, projectId);
	}

	public async Task<ProjectPermission> GetEffectivePermissionsAsync(Guid userId, Guid projectId)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		return await ProjectAccessQueries.EffectivePermissionsAsync(db, userId, projectId);
	}

	public async Task<bool> HasPermissionAsync(Guid userId, Guid projectId, ProjectPermission perm)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		return await ProjectAccessQueries.HasPermissionAsync(db, userId, projectId, perm);
	}

	public async Task<IReadOnlyList<Guid>> AudienceUserIdsAsync(Guid projectId)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		return await ProjectAccessQueries.AudienceUserIdsAsync(db, projectId);
	}
}
