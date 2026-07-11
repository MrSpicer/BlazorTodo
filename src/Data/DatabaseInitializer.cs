using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TodoList.Identity;

namespace TodoList.Data;

/// <summary>
/// Runs once at startup: applies pending EF Core migrations, then seeds the bootstrap admin
/// account (and its "Admin" role) from <see cref="AdminSeedOptions"/>. Kept idempotent so it's
/// safe on every boot; seed failures are logged rather than thrown so a bad configuration never
/// crash-loops the app.
/// </summary>
public sealed class DatabaseInitializer
{
	public const string AdminRole = "Admin";

	private readonly IDbContextFactory<AppDbContext> _contextFactory;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly RoleManager<IdentityRole<Guid>> _roleManager;
	private readonly AdminSeedOptions _options;
	private readonly ILogger<DatabaseInitializer> _logger;

	public DatabaseInitializer(
		IDbContextFactory<AppDbContext> contextFactory,
		UserManager<ApplicationUser> userManager,
		RoleManager<IdentityRole<Guid>> roleManager,
		IOptions<AdminSeedOptions> options,
		ILogger<DatabaseInitializer> logger)
	{
		_contextFactory = contextFactory;
		_userManager = userManager;
		_roleManager = roleManager;
		_options = options.Value;
		_logger = logger;
	}

	public async Task RunAsync(CancellationToken cancellationToken = default)
	{
		await MigrateAsync(cancellationToken);
		await SeedAdminAsync(cancellationToken);
	}

	private async Task MigrateAsync(CancellationToken cancellationToken)
	{
		await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
		await db.Database.MigrateAsync(cancellationToken);
		_logger.LogInformation("Database migrations applied.");
	}

	private async Task SeedAdminAsync(CancellationToken cancellationToken)
	{
		var email = _options.Email?.Trim();
		var password = ResolvePassword();

		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
		{
			_logger.LogInformation("Admin seeding skipped (AdminUser not configured).");
			return;
		}

		// Ensure the Admin role exists before assigning it.
		if (!await _roleManager.RoleExistsAsync(AdminRole))
		{
			var roleResult = await _roleManager.CreateAsync(new IdentityRole<Guid>(AdminRole));
			if (!roleResult.Succeeded)
			{
				_logger.LogError("Failed to create '{Role}' role: {Errors}", AdminRole, Describe(roleResult));
				return;
			}
		}

		var existing = await _userManager.FindByEmailAsync(email);
		if (existing is not null)
		{
			// Idempotent: never reset an existing account's password; just guarantee the role.
			if (!await _userManager.IsInRoleAsync(existing, AdminRole))
			{
				await _userManager.AddToRoleAsync(existing, AdminRole);
				_logger.LogInformation("Added existing user {Email} to the '{Role}' role.", email, AdminRole);
			}
			return;
		}

		var user = new ApplicationUser
		{
			Email = email,
			UserName = email,
			DisplayName = string.IsNullOrWhiteSpace(_options.DisplayName) ? "Administrator" : _options.DisplayName!.Trim(),
			// Pre-confirmed so the account can sign in despite RequireConfirmedEmail = true.
			EmailConfirmed = true,
			CreatedAt = DateTime.UtcNow,
		};

		var createResult = await _userManager.CreateAsync(user, password);
		if (!createResult.Succeeded)
		{
			// Most likely the password fails the length-10 + complexity rules. Surface it clearly.
			_logger.LogError("Failed to seed admin user {Email}: {Errors}", email, Describe(createResult));
			return;
		}

		var addRoleResult = await _userManager.AddToRoleAsync(user, AdminRole);
		if (!addRoleResult.Succeeded)
		{
			_logger.LogError("Seeded admin user {Email} but failed to assign '{Role}' role: {Errors}",
				email, AdminRole, Describe(addRoleResult));
			return;
		}

		_logger.LogInformation("Seeded admin user {Email} with the '{Role}' role.", email, AdminRole);
	}

	/// <summary>
	/// Prefers the password file (a Docker secret) when set and readable; otherwise falls back to
	/// the inline password. Trims trailing whitespace/newlines that secret tooling may append.
	/// </summary>
	private string? ResolvePassword()
	{
		var path = _options.PasswordFile?.Trim();
		if (!string.IsNullOrWhiteSpace(path))
		{
			try
			{
				if (File.Exists(path))
					return File.ReadAllText(path).Trim();

				_logger.LogWarning("AdminUser:PasswordFile '{Path}' does not exist; falling back to inline password.", path);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to read AdminUser:PasswordFile '{Path}'; falling back to inline password.", path);
			}
		}

		return _options.Password;
	}

	private static string Describe(IdentityResult result) =>
		string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
}
