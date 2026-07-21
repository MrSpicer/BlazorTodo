using Microsoft.EntityFrameworkCore;
using TodoList.Models;
using TodoList.Models.Enums;

namespace TodoList.Data;

/// <summary>
/// EF-translatable access predicates over a live <see cref="AppDbContext"/>. A user may access a
/// project they own OR one they are an accepted member of; what a member may <em>do</em> is
/// governed by the per-(project, role) <see cref="ProjectAccessRole.Permissions"/> grant (plus the
/// per-user <see cref="ProjectMember.CanManageAccess"/> override, which adds the member-management
/// bits). The owner implicitly has <see cref="ProjectPermission.All"/>. Composable
/// <see cref="IQueryable{T}"/> helpers let callers <c>.Contains(...)</c> them as subqueries and
/// stay a single round-trip.
/// </summary>
public static class ProjectAccessQueries
{
	public static IQueryable<Project> AccessibleProjects(AppDbContext db, Guid userId) =>
		db.Projects.Where(p =>
			p.UserId == userId ||
			db.ProjectMembers.Any(m =>
				m.ProjectId == p.Id && m.UserId == userId && m.Status == ProjectMemberStatus.Accepted));

	public static IQueryable<Guid> AccessibleProjectIds(AppDbContext db, Guid userId) =>
		AccessibleProjects(db, userId).Select(p => p.Id);

	/// <summary>
	/// Distinct owners of every project the user can access — used to resolve per-user reference
	/// data (custom statuses/priorities/tags) by owner so a member sees the owner's labels.
	/// For a solo user this is just themselves, so reference-data reads are unchanged.
	/// </summary>
	public static IQueryable<Guid> AccessibleOwnerIds(AppDbContext db, Guid userId) =>
		AccessibleProjects(db, userId).Select(p => p.UserId).Distinct();

	public static Task<bool> IsOwnerAsync(AppDbContext db, Guid userId, Guid projectId, CancellationToken ct = default) =>
		db.Projects.AnyAsync(p => p.Id == projectId && p.UserId == userId, ct);

	/// <summary>
	/// Every user who should be notified of a change in a project: the owner plus all accepted
	/// members. Used to fan out real-time change events beyond the acting user.
	/// </summary>
	public static async Task<List<Guid>> AudienceUserIdsAsync(AppDbContext db, Guid projectId, CancellationToken ct = default)
	{
		var owner = await db.Projects.Where(p => p.Id == projectId)
			.Select(p => p.UserId).FirstOrDefaultAsync(ct);
		var members = await db.ProjectMembers
			.Where(m => m.ProjectId == projectId && m.Status == ProjectMemberStatus.Accepted)
			.Select(m => m.UserId)
			.ToListAsync(ct);

		var set = new HashSet<Guid>(members);
		if (owner != Guid.Empty) set.Add(owner);
		return set.ToList();
	}

	/// <summary>
	/// The user's effective capabilities in a project: <see cref="ProjectPermission.All"/> for the
	/// owner; for an accepted member, their role's grant OR-ed with the member-management bits when
	/// their per-user <see cref="ProjectMember.CanManageAccess"/> override is set; otherwise
	/// <see cref="ProjectPermission.None"/>.
	/// </summary>
	public static async Task<ProjectPermission> EffectivePermissionsAsync(AppDbContext db, Guid userId, Guid projectId, CancellationToken ct = default)
	{
		if (await db.Projects.AnyAsync(p => p.Id == projectId && p.UserId == userId, ct))
			return ProjectPermission.All;

		var member = await db.ProjectMembers
			.Where(m => m.ProjectId == projectId && m.UserId == userId && m.Status == ProjectMemberStatus.Accepted)
			.Select(m => new { m.RoleId, m.CanManageAccess })
			.FirstOrDefaultAsync(ct);
		if (member is null) return ProjectPermission.None;

		var granted = await db.ProjectAccessRoles
			.Where(r => r.ProjectId == projectId && r.RoleId == member.RoleId)
			.Select(r => r.Permissions)
			.FirstOrDefaultAsync(ct);

		if (member.CanManageAccess)
			granted |= ProjectPermission.ManageMembers;

		return granted;
	}

	/// <summary>True when the user's effective permissions include every bit in <paramref name="perm"/>.</summary>
	public static async Task<bool> HasPermissionAsync(AppDbContext db, Guid userId, Guid projectId, ProjectPermission perm, CancellationToken ct = default)
	{
		var effective = await EffectivePermissionsAsync(db, userId, projectId, ct);
		return (effective & perm) == perm;
	}

	/// <summary>
	/// Ids of projects where the user has the given permission: every project they own, plus member
	/// projects whose role grant contains <paramref name="perm"/>. Used to scope content reads
	/// (e.g. only list todos in projects where the user has <see cref="ProjectPermission.TodosRead"/>).
	/// </summary>
	public static IQueryable<Guid> ProjectIdsWith(AppDbContext db, Guid userId, ProjectPermission perm) =>
		db.Projects.Where(p =>
			p.UserId == userId ||
			db.ProjectMembers.Any(m =>
				m.ProjectId == p.Id && m.UserId == userId && m.Status == ProjectMemberStatus.Accepted &&
				db.ProjectAccessRoles.Any(r =>
					r.ProjectId == p.Id && r.RoleId == m.RoleId && (r.Permissions & perm) == perm)))
		.Select(p => p.Id);

	/// <summary>
	/// Distinct owner ids of projects where the user has <paramref name="perm"/> — used to gate
	/// writes to the owner's shared reference data (tags/statuses/priorities) by permission.
	/// </summary>
	public static IQueryable<Guid> OwnerIdsWhereMemberHas(AppDbContext db, Guid userId, ProjectPermission perm) =>
		db.Projects.Where(p =>
			p.UserId == userId ||
			db.ProjectMembers.Any(m =>
				m.ProjectId == p.Id && m.UserId == userId && m.Status == ProjectMemberStatus.Accepted &&
				db.ProjectAccessRoles.Any(r =>
					r.ProjectId == p.Id && r.RoleId == m.RoleId && (r.Permissions & perm) == perm)))
		.Select(p => p.UserId).Distinct();

	/// <summary>
	/// Owner, or an accepted member who may invite/remove/change roles (any of the member-management
	/// bits below <see cref="ProjectPermission.MembersRead"/>). Gates the members-management UI and
	/// the invite/remove/change-role service calls.
	/// </summary>
	public static async Task<bool> CanManageAccessAsync(AppDbContext db, Guid userId, Guid projectId, CancellationToken ct = default)
	{
		var effective = await EffectivePermissionsAsync(db, userId, projectId, ct);
		const ProjectPermission manage = ProjectPermission.MembersInvite | ProjectPermission.MembersModify | ProjectPermission.MembersRemove;
		return (effective & manage) != 0;
	}
}
