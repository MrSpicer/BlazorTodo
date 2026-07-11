using TodoList.Data.Repositories;
using TodoList.Models;

namespace TodoList.Identity;

/// <summary>
/// Seeds a furnished starting state for a new authenticated user (default project, etc.).
/// Built-in statuses/priorities are not persisted — they live in-memory only — so this only
/// touches the per-user Projects bucket.
/// </summary>
public interface IUserOnboardingService
{
	Task SeedDefaultsAsync(Guid userId);
}

public class UserOnboardingService : IUserOnboardingService
{
	private readonly EfProjectRepository _projects;
	private readonly ILogger<UserOnboardingService> _logger;

	public UserOnboardingService(EfProjectRepository projects, ILogger<UserOnboardingService> logger)
	{
		_projects = projects;
		_logger = logger;
	}

	public async Task SeedDefaultsAsync(Guid userId)
	{
		if (userId == Guid.Empty)
			return;

		var existing = await _projects.GetProjects();
		if (existing.Count > 0)
		{
			// Idempotent: don't re-seed if the user already has a project.
			return;
		}

		var personal = new Project
		{
			Id = Guid.NewGuid(),
			UserId = userId,
			Name = "Personal",
			Description = "Your default project — a place to start.",
			Color = "#6366f1",
			IsDefault = true,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow,
		};

		var ok = await _projects.AddOrUpdate(personal);
		if (!ok)
			_logger.LogWarning("Failed to seed default project for user {UserId}", userId);
	}
}
