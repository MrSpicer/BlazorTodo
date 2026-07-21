using TodoList.Models.Enums;

namespace TodoList.Models;

/// <summary>
/// The per-(project, role) permission grant: the capabilities every accepted member assigned this
/// role has in the project. One row per (ProjectId, RoleId). The mere existence of a row no longer
/// implies "can manage access" — the <see cref="ProjectPermission.ManageMembers"/> bits do. The
/// project owner is never represented here; the owner implicitly has <see cref="ProjectPermission.All"/>.
/// Complemented by the per-user <see cref="ProjectMember.CanManageAccess"/> override.
/// </summary>
public class ProjectAccessRole
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid ProjectId { get; set; }

	public Guid RoleId { get; set; }

	/// <summary>The capabilities granted to this role in this project.</summary>
	public ProjectPermission Permissions { get; set; } = ProjectPermission.None;
}
