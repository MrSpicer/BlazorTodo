using TodoList.Models.Enums;

namespace TodoList.Services.Access;

/// <summary>
/// Standalone (non-repository) access checks for the service and UI layers. Each call opens a
/// short-lived <see cref="TodoList.Data.AppDbContext"/> from the factory and delegates to
/// <see cref="TodoList.Data.ProjectAccessQueries"/>.
/// </summary>
public interface IProjectAccessResolver
{
	Task<IReadOnlyList<Guid>> AccessibleProjectIdsAsync(Guid userId);
	Task<bool> IsOwnerAsync(Guid userId, Guid projectId);
	Task<bool> CanManageAccessAsync(Guid userId, Guid projectId);

	/// <summary>The user's effective capabilities in a project (used by the UI to gate actions).</summary>
	Task<ProjectPermission> GetEffectivePermissionsAsync(Guid userId, Guid projectId);
	/// <summary>True when the user's effective permissions include every bit in <paramref name="perm"/>.</summary>
	Task<bool> HasPermissionAsync(Guid userId, Guid projectId, ProjectPermission perm);

	/// <summary>Owner + accepted members of a project, for fanning out real-time change events.</summary>
	Task<IReadOnlyList<Guid>> AudienceUserIdsAsync(Guid projectId);
}
