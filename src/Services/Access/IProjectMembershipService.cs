using TodoList.Models.Enums;

namespace TodoList.Services.Access;

/// <summary>
/// Owner/delegate operations for sharing a project: invite existing accounts by email, accept an
/// invitation, and manage members, roles, and per-role permissions. All mutating calls re-check
/// that <c>actingUserId</c> is permitted.
/// </summary>
public interface IProjectMembershipService
{
	Task<MembershipResult> InviteAsync(Guid projectId, Guid actingUserId, string email, Guid roleId, string origin);
	Task<MembershipResult> AcceptAsync(Guid userId, string token);

	Task<IReadOnlyList<ProjectMemberView>> GetMembersAsync(Guid projectId);
	Task<MembershipResult> RemoveMemberAsync(Guid projectId, Guid actingUserId, Guid memberUserId);
	Task<MembershipResult> ChangeRoleAsync(Guid projectId, Guid actingUserId, Guid memberUserId, Guid roleId);
	Task<MembershipResult> SetCanManageAccessAsync(Guid projectId, Guid actingUserId, Guid memberUserId, bool value);

	/// <summary>The per-role permission grants for a project — one entry per role assigned to a
	/// member (or already granted). Drives the matrix editor.</summary>
	Task<IReadOnlyList<RolePermissionView>> GetRolePermissionsAsync(Guid projectId);
	/// <summary>Set a role's permission grant in a project. Owner-only (prevents privilege escalation).</summary>
	Task<MembershipResult> SetRolePermissionsAsync(Guid projectId, Guid actingUserId, Guid roleId, ProjectPermission permissions);

	Task<bool> CanManageAccessAsync(Guid projectId, Guid userId);
	/// <summary>The user's effective capabilities in a project (for UI action gating).</summary>
	Task<ProjectPermission> GetEffectivePermissionsAsync(Guid projectId, Guid userId);
}
