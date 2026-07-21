using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using TodoList.Data;
using TodoList.Identity;

namespace TodoList.Services.Admin;

/// <summary>
/// <see cref="IAdminService"/> backed by Identity's <see cref="UserManager{TUser}"/> and the
/// EF Core context. Confirmation/reset emails point at the default Identity UI pages, encoding
/// tokens exactly as those pages expect (Base64Url of the UTF-8 token).
/// </summary>
public sealed class AdminService : IAdminService
{
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly RoleManager<IdentityRole<Guid>> _roleManager;
	private readonly IDbContextFactory<AppDbContext> _dbFactory;
	private readonly IEmailSender _emailSender;
	private readonly ILogger<AdminService> _logger;

	public AdminService(
		UserManager<ApplicationUser> userManager,
		RoleManager<IdentityRole<Guid>> roleManager,
		IDbContextFactory<AppDbContext> dbFactory,
		IEmailSender emailSender,
		ILogger<AdminService> logger)
	{
		_userManager = userManager;
		_roleManager = roleManager;
		_dbFactory = dbFactory;
		_emailSender = emailSender;
		_logger = logger;
	}

	public async Task<IReadOnlyList<AdminUserView>> GetUsersAsync()
	{
		try
		{
			var users = await _userManager.Users
				.OrderBy(u => u.Email)
				.ToListAsync();

			var views = new List<AdminUserView>(users.Count);
			foreach (var u in users)
			{
				var roles = (await _userManager.GetRolesAsync(u)).ToList();
				views.Add(new AdminUserView(
					u.Id,
					u.Email ?? string.Empty,
					u.DisplayName,
					u.EmailConfirmed,
					u.LockoutEnd,
					u.AccessFailedCount,
					u.CreatedAt,
					roles));
			}

			return views;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to load users for admin dashboard.");
			return Array.Empty<AdminUserView>();
		}
	}

	public async Task<IReadOnlyList<string>> GetRolesAsync()
	{
		try
		{
			return await _roleManager.Roles
				.Select(r => r.Name!)
				.OrderBy(n => n)
				.ToListAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to load roles for admin dashboard.");
			return Array.Empty<string>();
		}
	}

	public async Task<AdminResult> CreateRoleAsync(string name)
	{
		try
		{
			name = name?.Trim() ?? string.Empty;
			if (name.Length == 0)
				return AdminResult.Fail("Role name is required.");
			if (name.Length > 256)
				return AdminResult.Fail("Role name is too long.");

			if (await _roleManager.RoleExistsAsync(name))
				return AdminResult.Fail("A role with that name already exists.");

			var result = await _roleManager.CreateAsync(new IdentityRole<Guid>(name));
			if (!result.Succeeded)
				return AdminResult.Fail("Could not create the role.");

			_logger.LogInformation("Admin created role {Role}.", name);
			return AdminResult.Ok($"Created role '{name}'.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create role {Role}.", name);
			return AdminResult.Fail("Could not create the role.");
		}
	}

	public async Task<AdminResult> SetUserRolesAsync(Guid userId, IReadOnlyList<string> roleNames, Guid actingAdminId)
	{
		try
		{
			var user = await _userManager.FindByIdAsync(userId.ToString());
			if (user is null)
				return AdminResult.Fail("User not found.");

			var current = await _userManager.GetRolesAsync(user);
			var toAdd = roleNames.Except(current).ToList();
			var toRemove = current.Except(roleNames).ToList();

			if (toAdd.Count == 0 && toRemove.Count == 0)
				return AdminResult.Ok("No role changes.");

			// Guard the Admin role the same way DeleteUserAsync does, so the site can't be
			// locked out of its own dashboard.
			if (toRemove.Contains(DatabaseInitializer.AdminRole))
			{
				if (userId == actingAdminId)
					return AdminResult.Fail("You cannot remove your own admin role.");

				var admins = await _userManager.GetUsersInRoleAsync(DatabaseInitializer.AdminRole);
				if (admins.Count <= 1)
					return AdminResult.Fail("You cannot remove the last remaining admin.");
			}

			// Ignore any role that doesn't actually exist to avoid Identity errors.
			var addable = new List<string>();
			foreach (var role in toAdd)
			{
				if (await _roleManager.RoleExistsAsync(role))
					addable.Add(role);
			}

			var email = user.Email ?? user.Id.ToString();

			if (addable.Count > 0)
			{
				var addResult = await _userManager.AddToRolesAsync(user, addable);
				if (!addResult.Succeeded)
					return AdminResult.Fail("Could not grant one or more roles.");
			}

			if (toRemove.Count > 0)
			{
				var removeResult = await _userManager.RemoveFromRolesAsync(user, toRemove);
				if (!removeResult.Succeeded)
					return AdminResult.Fail("Could not revoke one or more roles.");
			}

			_logger.LogInformation("Admin {Admin} updated roles for {Email} ({UserId}). Granted: [{Added}]; revoked: [{Removed}].",
				actingAdminId, email, userId, string.Join(", ", addable), string.Join(", ", toRemove));
			return AdminResult.Ok($"Updated roles for {email}.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to update roles for user {UserId}.", userId);
			return AdminResult.Fail("Could not update the user's roles.");
		}
	}

	public async Task<AdminResult> ConfirmEmailAsync(Guid userId)
	{
		try
		{
			var user = await _userManager.FindByIdAsync(userId.ToString());
			if (user is null)
				return AdminResult.Fail("User not found.");

			if (user.EmailConfirmed)
				return AdminResult.Ok("Email is already confirmed.");

			user.EmailConfirmed = true;
			var result = await _userManager.UpdateAsync(user);
			return result.Succeeded
				? AdminResult.Ok("Email marked as confirmed.")
				: AdminResult.Fail("Could not update the account.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to confirm email for user {UserId}.", userId);
			return AdminResult.Fail("Something went wrong confirming the email.");
		}
	}

	public async Task<AdminResult> ResendConfirmationAsync(Guid userId, string origin)
	{
		try
		{
			var user = await _userManager.FindByIdAsync(userId.ToString());
			if (user is null)
				return AdminResult.Fail("User not found.");
			if (user.EmailConfirmed)
				return AdminResult.Ok("Email is already confirmed; nothing sent.");
			if (string.IsNullOrWhiteSpace(user.Email))
				return AdminResult.Fail("User has no email address.");

			var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
			var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
			var link = $"{origin.TrimEnd('/')}/Identity/Account/ConfirmEmail" +
				$"?userId={Uri.EscapeDataString(user.Id.ToString())}&code={Uri.EscapeDataString(code)}";

			await _emailSender.SendEmailAsync(
				user.Email,
				"Confirm your email",
				$"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(link)}'>clicking here</a>.");

			return AdminResult.Ok($"Confirmation email sent to {user.Email}.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to resend confirmation for user {UserId}.", userId);
			return AdminResult.Fail("Could not send the confirmation email.");
		}
	}

	public async Task<AdminResult> SendPasswordResetAsync(Guid userId, string origin)
	{
		try
		{
			var user = await _userManager.FindByIdAsync(userId.ToString());
			if (user is null)
				return AdminResult.Fail("User not found.");
			if (string.IsNullOrWhiteSpace(user.Email))
				return AdminResult.Fail("User has no email address.");

			var token = await _userManager.GeneratePasswordResetTokenAsync(user);
			var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
			var link = $"{origin.TrimEnd('/')}/Identity/Account/ResetPassword" +
				$"?code={Uri.EscapeDataString(code)}";

			await _emailSender.SendEmailAsync(
				user.Email,
				"Reset your password",
				$"Reset your password by <a href='{HtmlEncoder.Default.Encode(link)}'>clicking here</a>.");

			return AdminResult.Ok($"Password reset email sent to {user.Email}.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to send password reset for user {UserId}.", userId);
			return AdminResult.Fail("Could not send the password reset email.");
		}
	}

	public async Task<AdminResult> DeleteUserAsync(Guid userId, Guid actingAdminId)
	{
		try
		{
			if (userId == actingAdminId)
				return AdminResult.Fail("You cannot delete your own account.");

			var user = await _userManager.FindByIdAsync(userId.ToString());
			if (user is null)
				return AdminResult.Fail("User not found.");

			// Guard the last admin so the site can't be locked out of its own dashboard.
			if (await _userManager.IsInRoleAsync(user, DatabaseInitializer.AdminRole))
			{
				var admins = await _userManager.GetUsersInRoleAsync(DatabaseInitializer.AdminRole);
				if (admins.Count <= 1)
					return AdminResult.Fail("You cannot delete the last remaining admin.");
			}

			var email = user.Email ?? user.Id.ToString();

			// One transaction removes all owned rows plus the Identity user. Delete child tables
			// (todos/notes reference projects) before projects; the AspNetUser* join tables
			// cascade from AspNetUsers at the DB level, so deleting the user row cleans them up.
			await using var db = await _dbFactory.CreateDbContextAsync();
			await using var tx = await db.Database.BeginTransactionAsync();

			await db.Todos.Where(t => t.UserId == userId).ExecuteDeleteAsync();
			await db.Notes.Where(n => n.UserId == userId).ExecuteDeleteAsync();
			await db.FilterPresets.Where(f => f.UserId == userId).ExecuteDeleteAsync();
			await db.Projects.Where(p => p.UserId == userId).ExecuteDeleteAsync();
			await db.Tags.Where(t => t.UserId == userId).ExecuteDeleteAsync();
			await db.Statuses.Where(s => s.UserId == userId).ExecuteDeleteAsync();
			await db.Priorities.Where(p => p.UserId == userId).ExecuteDeleteAsync();
			await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();

			await tx.CommitAsync();

			_logger.LogInformation("Admin {Admin} deleted user {Email} ({UserId}) and all their data.",
				actingAdminId, email, userId);
			return AdminResult.Ok($"Deleted {email} and all their data.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to delete user {UserId}.", userId);
			return AdminResult.Fail("Could not delete the user.");
		}
	}
}
