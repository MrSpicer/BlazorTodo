using TodoList.Data;
using TodoList.Identity;
using TodoList.Models;
using TodoList.Realtime;
using TodoList.Services.Access;

namespace TodoList.Services;

public class ProjectService : EntityServiceBase<Project>, IProjectService
{
	private readonly ICurrentUserContext _user;
	private readonly IUserOnboardingService _onboarding;
	private readonly IUserChangeBus _bus;
	private readonly IProjectAccessResolver _access;
	private Project? _selectedProject;

	public event Action? OnProjectsChanged
	{
		add => OnChanged += value;
		remove => OnChanged -= value;
	}

	public IReadOnlyList<Project> Projects => Items;
	public Project? SelectedProject => _selectedProject;

	public ProjectService(
		IProjectRepository repository,
		ICurrentUserContext user,
		IUserOnboardingService onboarding,
		IUserChangeBus bus,
		IProjectAccessResolver access,
		ILogger<ProjectService> logger)
		: base((IRepository<Project>)repository, logger)
	{
		_user = user;
		_onboarding = onboarding;
		_bus = bus;
		_access = access;
	}

	public async Task RefreshAsync()
	{
		_items = (await Repository.GetAll()).OrderBy(p => p.CreatedAt).ToList();
		if (_selectedProject is null || _items.All(p => p.Id != _selectedProject.Id))
			_selectedProject = _items.FirstOrDefault(p => p.IsDefault) ?? _items.FirstOrDefault();
		NotifyChanged();
	}

	// Fan out to every user who can see the project (owner + accepted members), so a change made
	// by one collaborator refreshes the others' open circuits — not just the acting user's.
	private async Task PublishChange(Guid projectId)
	{
		if (!_user.IsAuthenticated) return;
		await PublishToAudience(await _access.AudienceUserIdsAsync(projectId));
	}

	private async Task PublishToAudience(IReadOnlyList<Guid> audience)
	{
		foreach (var userId in audience)
			await _bus.PublishAsync(new UserChangeEvent(userId, ChangeKind.Projects));
	}

	public override async Task InitializeAsync()
	{
		await Repository.InitializeAsync();
		var loaded = (await Repository.GetAll()).OrderBy(p => p.CreatedAt).ToList();

		// First authenticated load with no projects → seed a default "Personal" project.
		// Idempotent: returns immediately if any project already exists for the user.
		if (loaded.Count == 0 && _user.IsAuthenticated)
		{
			await _onboarding.SeedDefaultsAsync(_user.UserId);
			loaded = (await Repository.GetAll()).OrderBy(p => p.CreatedAt).ToList();
		}

		_items = loaded;
		_selectedProject = _items.FirstOrDefault(p => p.IsDefault) ?? _items.FirstOrDefault();
		NotifyChanged();
	}

	public async Task<bool> SaveProjectAsync(Project project)
	{
		project.UpdatedAt = DateTime.UtcNow;
		var success = await Repository.AddOrUpdate(project);
		if (success)
		{
			var idx = _items.FindIndex(p => p.Id == project.Id);
			if (idx >= 0)
				_items[idx] = project;
			else
				_items.Add(project);
			NotifyChanged();
			await PublishChange(project.Id);
		}
		return success;
	}

	public async Task DeleteProjectAsync(Project project)
	{
		// Capture the audience before deletion cascades away the membership rows.
		var audience = _user.IsAuthenticated
			? await _access.AudienceUserIdsAsync(project.Id)
			: (IReadOnlyList<Guid>)Array.Empty<Guid>();

		await Repository.Delete(project);
		_items.RemoveAll(p => p.Id == project.Id);

		if (_selectedProject?.Id == project.Id)
			_selectedProject = _items.FirstOrDefault();

		NotifyChanged();
		await PublishToAudience(audience);
	}

	public void SelectProject(Project? project)
	{
		_selectedProject = project;
		NotifyChanged();
	}

	public Project? GetDefaultProject() => _items.FirstOrDefault(p => p.IsDefault);

	public int GetTodoCount(Guid projectId, IReadOnlyList<TodoItem> todos) =>
		todos.Count(t => t.ProjectId == projectId);
}
