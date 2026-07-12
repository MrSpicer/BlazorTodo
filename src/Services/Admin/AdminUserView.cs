namespace TodoList.Services.Admin;

/// <summary>
/// Read-only projection of an account for the admin dashboard. Deliberately carries only
/// metadata — never any todo/note contents.
/// </summary>
public sealed record AdminUserView(
	Guid Id,
	string Email,
	string DisplayName,
	bool EmailConfirmed,
	DateTimeOffset? LockoutEnd,
	int AccessFailedCount,
	DateTime CreatedAt,
	bool IsAdmin)
{
	/// <summary>True when the account is currently locked out.</summary>
	public bool IsLockedOut => LockoutEnd is { } end && end > DateTimeOffset.UtcNow;
}
