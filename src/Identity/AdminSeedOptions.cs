namespace TodoList.Identity;

/// <summary>
/// Deploy-time configuration for the bootstrap admin account, bound from the "AdminUser"
/// configuration section. When no email/password is supplied, admin seeding is skipped
/// entirely (e.g. local `dotnet run` without an AdminUser block).
/// </summary>
public sealed class AdminSeedOptions
{
	public string? Email { get; set; }
	public string? DisplayName { get; set; }

	/// <summary>Inline password — convenient for local dev.</summary>
	public string? Password { get; set; }

	/// <summary>
	/// Path to a file holding the password (a Docker secret mounted under /run/secrets in
	/// production). Takes precedence over <see cref="Password"/> when set and readable.
	/// </summary>
	public string? PasswordFile { get; set; }
}
