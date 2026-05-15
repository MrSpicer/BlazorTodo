using Microsoft.EntityFrameworkCore;
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

	protected Guid RequireUserId()
	{
		if (!_user.IsAuthenticated)
			throw new InvalidOperationException("EF repository called without an authenticated user.");
		return _user.UserId;
	}

	protected Task<AppDbContext> CreateDbAsync(CancellationToken ct = default)
		=> _dbFactory.CreateDbContextAsync(ct);
}
