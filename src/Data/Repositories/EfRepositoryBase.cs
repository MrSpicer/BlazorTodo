using Microsoft.EntityFrameworkCore;
using TodoList.Helpers;
using TodoList.Identity;

namespace TodoList.Data.Repositories;

public abstract class EfRepositoryBase
{
	protected readonly IDbContextFactory<AppDbContext> _dbFactory;
	protected readonly ICurrentUserContext _user;

	protected EfRepositoryBase(IDbContextFactory<AppDbContext> dbFactory, ICurrentUserContext user)
	{
		_dbFactory = dbFactory;
		_user = user;
	}

	/// <summary>
	/// Defense-in-depth validation at the persistence boundary. The entity <c>IsValid()</c>
	/// checks only identity/required fields; this additionally enforces every DataAnnotation
	/// (max lengths, hex-colour regex, etc.) so no code path — form, import, or migration —
	/// can persist a malformed row to the shared database, regardless of upstream validation.
	/// </summary>
	protected static bool PassesDataAnnotations<T>(T entity) where T : class
		=> EntityValidation.Passes(entity);

	protected Guid RequireUserId()
	{
		if (!_user.IsAuthenticated)
			throw new InvalidOperationException("EF repository called without an authenticated user.");
		return _user.UserId;
	}

	protected Task<AppDbContext> CreateDbAsync(CancellationToken ct = default)
		=> _dbFactory.CreateDbContextAsync(ct);
}
