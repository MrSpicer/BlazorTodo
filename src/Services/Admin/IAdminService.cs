namespace TodoList.Services.Admin;

/// <summary>Outcome of an admin operation, carrying a user-facing message.</summary>
public sealed record AdminResult(bool Success, string Message)
{
	public static AdminResult Ok(string message) => new(true, message);
	public static AdminResult Fail(string message) => new(false, message);
}

/// <summary>
/// Admin user-management operations. All methods are safe to call only from the Admin-gated
/// dashboard; they follow the repo convention of catching at the boundary, logging, and
/// returning a result rather than throwing.
/// </summary>
public interface IAdminService
{
	/// <summary>All accounts, metadata only, ordered by email.</summary>
	Task<IReadOnlyList<AdminUserView>> GetUsersAsync();

	/// <summary>All role names known to the app, ordered alphabetically.</summary>
	Task<IReadOnlyList<string>> GetRolesAsync();

	/// <summary>
	/// Sets the account's roles to exactly <paramref name="roleNames"/> (the service computes the
	/// add/remove diff). Refuses to remove the Admin role from the acting admin or the last
	/// remaining admin.
	/// </summary>
	Task<AdminResult> SetUserRolesAsync(Guid userId, IReadOnlyList<string> roleNames, Guid actingAdminId);

	/// <summary>Marks the account's email as confirmed.</summary>
	Task<AdminResult> ConfirmEmailAsync(Guid userId);

	/// <summary>Emails a fresh confirmation link. <paramref name="origin"/> is the site base URL.</summary>
	Task<AdminResult> ResendConfirmationAsync(Guid userId, string origin);

	/// <summary>Emails a password-reset link. <paramref name="origin"/> is the site base URL.</summary>
	Task<AdminResult> SendPasswordResetAsync(Guid userId, string origin);

	/// <summary>
	/// Deletes the account and every row it owns across the domain tables. Refuses to delete the
	/// acting admin's own account or the last remaining admin.
	/// </summary>
	Task<AdminResult> DeleteUserAsync(Guid userId, Guid actingAdminId);
}
