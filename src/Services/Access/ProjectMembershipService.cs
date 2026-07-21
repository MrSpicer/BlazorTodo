using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using TodoList.Data;
using TodoList.Identity;
using TodoList.Models;
using TodoList.Models.Enums;

namespace TodoList.Services.Access;

public class ProjectMembershipService : IProjectMembershipService
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly RoleManager<IdentityRole<Guid>> _roleManager;
	private readonly IEmailSender _emailSender;
	private readonly ILogger<ProjectMembershipService> _logger;

	public ProjectMembershipService(
		IDbContextFactory<AppDbContext> dbFactory,
		UserManager<ApplicationUser> userManager,
		RoleManager<IdentityRole<Guid>> roleManager,
		IEmailSender emailSender,
		ILogger<ProjectMembershipService> logger)
	{
		_dbFactory = dbFactory;
		_userManager = userManager;
		_roleManager = roleManager;
		_emailSender = emailSender;
		_logger = logger;
	}

	public async Task<bool> CanManageAccessAsync(Guid projectId, Guid userId)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		return await ProjectAccessQueries.CanManageAccessAsync(db, userId, projectId);
	}

	public async Task<MembershipResult> InviteAsync(Guid projectId, Guid actingUserId, string email, Guid roleId, string origin)
	{
		try
		{
			email = (email ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(email))
				return MembershipResult.Fail("An email address is required.");

			await using var db = await _dbFactory.CreateDbContextAsync();

			var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
			if (project is null)
				return MembershipResult.Fail("Project not found.");
			if (!await ProjectAccessQueries.CanManageAccessAsync(db, actingUserId, projectId))
				return MembershipResult.Fail("You do not have permission to manage this project's members.");

			var role = await _roleManager.FindByIdAsync(roleId.ToString());
			if (role is null)
				return MembershipResult.Fail("The selected role no longer exists.");

			var invitee = await _userManager.FindByEmailAsync(email);
			if (invitee is null)
				return MembershipResult.Fail($"No account exists for {email}. Invitees must already have an account.");

			if (invitee.Id == project.UserId)
				return MembershipResult.Fail("That user already owns this project.");

			var existing = await db.ProjectMembers
				.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == invitee.Id);
			if (existing is { Status: ProjectMemberStatus.Accepted })
				return MembershipResult.Fail("That user is already a member of this project.");

			var token = GenerateToken();
			if (existing is null)
			{
				db.ProjectMembers.Add(new ProjectMember
				{
					Id = Guid.NewGuid(),
					ProjectId = projectId,
					UserId = invitee.Id,
					RoleId = roleId,
					Status = ProjectMemberStatus.Pending,
					InviteToken = token,
					InvitedByUserId = actingUserId,
					CreatedAt = DateTime.UtcNow
				});
			}
			else
			{
				// Re-invite a pending/declined member: refresh the role and token.
				existing.RoleId = roleId;
				existing.Status = ProjectMemberStatus.Pending;
				existing.InviteToken = token;
				existing.InvitedByUserId = actingUserId;
				existing.UpdatedAt = DateTime.UtcNow;
			}
			await db.SaveChangesAsync();

			await SendInviteEmailAsync(invitee.Email!, project.Name, token, origin);
			return MembershipResult.Ok($"Invitation sent to {email}.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to invite {Email} to project {ProjectId}.", email, projectId);
			return MembershipResult.Fail("Could not send the invitation.");
		}
	}

	public async Task<MembershipResult> AcceptAsync(Guid userId, string token)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(token))
				return MembershipResult.Fail("Invalid invitation link.");

			await using var db = await _dbFactory.CreateDbContextAsync();
			var member = await db.ProjectMembers.FirstOrDefaultAsync(m => m.InviteToken == token);
			if (member is null)
				return MembershipResult.Fail("This invitation is no longer valid.");
			if (member.UserId != userId)
				return MembershipResult.Fail("This invitation was sent to a different account.");

			if (member.Status != ProjectMemberStatus.Accepted)
			{
				member.Status = ProjectMemberStatus.Accepted;
				member.AcceptedAt = DateTime.UtcNow;
				member.UpdatedAt = DateTime.UtcNow;
				member.InviteToken = string.Empty; // single-use
				await db.SaveChangesAsync();
			}

			var name = await db.Projects.Where(p => p.Id == member.ProjectId)
				.Select(p => p.Name).FirstOrDefaultAsync() ?? "the project";
			return MembershipResult.Ok($"You now have access to {name}.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to accept invitation for user {UserId}.", userId);
			return MembershipResult.Fail("Could not accept the invitation.");
		}
	}

	public async Task<IReadOnlyList<ProjectMemberView>> GetMembersAsync(Guid projectId)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
		if (project is null)
			return Array.Empty<ProjectMemberView>();

		var members = await db.ProjectMembers.Where(m => m.ProjectId == projectId).ToListAsync();

		var userIds = members.Select(m => m.UserId).Append(project.UserId).Distinct().ToList();
		var users = await db.Users.Where(u => userIds.Contains(u.Id))
			.ToDictionaryAsync(u => u.Id);
		var roleIds = members.Select(m => m.RoleId).Distinct().ToList();
		var roles = await db.Roles.Where(r => roleIds.Contains(r.Id))
			.ToDictionaryAsync(r => r.Id, r => r.Name ?? string.Empty);

		var result = new List<ProjectMemberView>();

		users.TryGetValue(project.UserId, out var owner);
		result.Add(new ProjectMemberView
		{
			UserId = project.UserId,
			Email = owner?.Email ?? string.Empty,
			DisplayName = owner?.DisplayName ?? string.Empty,
			RoleName = "Owner",
			Status = ProjectMemberStatus.Accepted,
			CanManageAccess = true,
			IsOwner = true
		});

		foreach (var m in members.OrderBy(m => m.CreatedAt))
		{
			users.TryGetValue(m.UserId, out var u);
			result.Add(new ProjectMemberView
			{
				UserId = m.UserId,
				Email = u?.Email ?? string.Empty,
				DisplayName = u?.DisplayName ?? string.Empty,
				RoleId = m.RoleId,
				RoleName = roles.TryGetValue(m.RoleId, out var rn) ? rn : string.Empty,
				Status = m.Status,
				CanManageAccess = m.CanManageAccess,
				IsOwner = false
			});
		}

		return result;
	}

	public async Task<MembershipResult> RemoveMemberAsync(Guid projectId, Guid actingUserId, Guid memberUserId)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		if (!await ProjectAccessQueries.CanManageAccessAsync(db, actingUserId, projectId))
			return MembershipResult.Fail("You do not have permission to manage this project's members.");

		await db.ProjectMembers
			.Where(m => m.ProjectId == projectId && m.UserId == memberUserId)
			.ExecuteDeleteAsync();
		return MembershipResult.Ok("Member removed.");
	}

	public async Task<MembershipResult> ChangeRoleAsync(Guid projectId, Guid actingUserId, Guid memberUserId, Guid roleId)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		if (!await ProjectAccessQueries.CanManageAccessAsync(db, actingUserId, projectId))
			return MembershipResult.Fail("You do not have permission to manage this project's members.");

		var role = await _roleManager.FindByIdAsync(roleId.ToString());
		if (role is null)
			return MembershipResult.Fail("The selected role no longer exists.");

		var member = await db.ProjectMembers
			.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == memberUserId);
		if (member is null)
			return MembershipResult.Fail("Member not found.");

		member.RoleId = roleId;
		member.UpdatedAt = DateTime.UtcNow;
		await db.SaveChangesAsync();
		return MembershipResult.Ok("Role updated.");
	}

	public async Task<MembershipResult> SetCanManageAccessAsync(Guid projectId, Guid actingUserId, Guid memberUserId, bool value)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		if (!await ProjectAccessQueries.IsOwnerAsync(db, actingUserId, projectId))
			return MembershipResult.Fail("Only the project owner can delegate member management.");

		var member = await db.ProjectMembers
			.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == memberUserId);
		if (member is null)
			return MembershipResult.Fail("Member not found.");

		member.CanManageAccess = value;
		member.UpdatedAt = DateTime.UtcNow;
		await db.SaveChangesAsync();
		return MembershipResult.Ok("Access updated.");
	}

	public async Task<IReadOnlyList<RolePermissionView>> GetRolePermissionsAsync(Guid projectId)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();

		// Every role assigned to a member of the project, plus any role that already has a grant row.
		var memberRoleIds = await db.ProjectMembers
			.Where(m => m.ProjectId == projectId)
			.Select(m => m.RoleId).ToListAsync();
		var grants = await db.ProjectAccessRoles
			.Where(r => r.ProjectId == projectId)
			.ToDictionaryAsync(r => r.RoleId, r => r.Permissions);

		var roleIds = memberRoleIds.Concat(grants.Keys).Distinct().ToList();
		var roleNames = await db.Roles.Where(r => roleIds.Contains(r.Id))
			.ToDictionaryAsync(r => r.Id, r => r.Name ?? string.Empty);

		return roleIds
			.Select(id => new RolePermissionView
			{
				RoleId = id,
				RoleName = roleNames.TryGetValue(id, out var n) ? n : string.Empty,
				Permissions = grants.TryGetValue(id, out var p) ? p : ProjectPermission.None
			})
			.OrderBy(v => v.RoleName)
			.ToList();
	}

	public async Task<MembershipResult> SetRolePermissionsAsync(Guid projectId, Guid actingUserId, Guid roleId, ProjectPermission permissions)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		// Editing the permission matrix is owner-only so a delegate cannot escalate their own access.
		if (!await ProjectAccessQueries.IsOwnerAsync(db, actingUserId, projectId))
			return MembershipResult.Fail("Only the project owner can change role permissions.");

		var row = await db.ProjectAccessRoles
			.FirstOrDefaultAsync(r => r.ProjectId == projectId && r.RoleId == roleId);
		if (row is null)
		{
			db.ProjectAccessRoles.Add(new ProjectAccessRole
			{
				Id = Guid.NewGuid(),
				ProjectId = projectId,
				RoleId = roleId,
				Permissions = permissions
			});
		}
		else
		{
			row.Permissions = permissions;
		}
		await db.SaveChangesAsync();
		return MembershipResult.Ok("Permissions updated.");
	}

	public async Task<ProjectPermission> GetEffectivePermissionsAsync(Guid projectId, Guid userId)
	{
		await using var db = await _dbFactory.CreateDbContextAsync();
		return await ProjectAccessQueries.EffectivePermissionsAsync(db, userId, projectId);
	}

	private async Task SendInviteEmailAsync(string email, string projectName, string token, string origin)
	{
		var link = $"{origin.TrimEnd('/')}/invitations/accept?token={Uri.EscapeDataString(token)}";
		var safeProject = HtmlEncoder.Default.Encode(projectName);
		await _emailSender.SendEmailAsync(
			email,
			$"You've been invited to \"{projectName}\"",
			$"You've been invited to collaborate on <strong>{safeProject}</strong>. " +
			$"<a href='{HtmlEncoder.Default.Encode(link)}'>Click here to accept</a>.");
	}

	private static string GenerateToken() =>
		Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
