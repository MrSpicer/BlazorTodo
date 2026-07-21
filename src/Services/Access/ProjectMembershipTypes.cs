using TodoList.Models.Enums;

namespace TodoList.Services.Access;

/// <summary>Result of a membership mutation, mirroring the AdminResult convention.</summary>
public record MembershipResult(bool Succeeded, string Message)
{
	public static MembershipResult Ok(string message = "") => new(true, message);
	public static MembershipResult Fail(string message) => new(false, message);
}

/// <summary>A row for the members-management UI: the owner plus each invited member.</summary>
public class ProjectMemberView
{
	public Guid UserId { get; init; }
	public string Email { get; init; } = string.Empty;
	public string DisplayName { get; init; } = string.Empty;
	public Guid RoleId { get; init; }
	public string RoleName { get; init; } = string.Empty;
	public ProjectMemberStatus Status { get; init; }
	public bool CanManageAccess { get; init; }
	public bool IsOwner { get; init; }
}

/// <summary>A row for the per-project role permission matrix editor.</summary>
public class RolePermissionView
{
	public Guid RoleId { get; init; }
	public string RoleName { get; init; } = string.Empty;
	public ProjectPermission Permissions { get; set; }
}
