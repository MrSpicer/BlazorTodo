using TodoList.Models.Enums;

namespace TodoList.Models;

/// <summary>
/// A user's membership in a project they do not own. The project owner is the
/// <see cref="Project.UserId"/> and does not get a row here. A member is created in the
/// <see cref="ProjectMemberStatus.Pending"/> state at invite time and becomes
/// <see cref="ProjectMemberStatus.Accepted"/> once the invitee follows the emailed accept link.
/// </summary>
public class ProjectMember : IEntity
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid ProjectId { get; set; }

	/// <summary>The member's Identity user id (an existing account).</summary>
	public Guid UserId { get; set; }

	/// <summary>The global Identity role assigned to this member by the owner at invite time.</summary>
	public Guid RoleId { get; set; }

	public ProjectMemberStatus Status { get; set; } = ProjectMemberStatus.Pending;

	/// <summary>Random token embedded in the invite email's accept link.</summary>
	public string InviteToken { get; set; } = string.Empty;

	public Guid InvitedByUserId { get; set; }

	/// <summary>Per-user delegation: when true this member may manage the project's members/access.</summary>
	public bool CanManageAccess { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAt { get; set; }
	public DateTime? AcceptedAt { get; set; }

	public bool IsValid() =>
		Id != Guid.Empty && ProjectId != Guid.Empty && UserId != Guid.Empty && RoleId != Guid.Empty;
}
