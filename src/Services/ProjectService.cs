using TodoList.Data;
using TodoList.Identity;
using TodoList.Models;
using TodoList.Realtime;

namespace TodoList.Services;

public class ProjectService : EntityServiceBase<Project>, IProjectService
{
	private readonly ICurrentUserContext _user;
	private readonly IUserOnboardingService _onboarding;
	private readonly IUserChangeBus _bus;
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
		ILogger<ProjectService> logger)
		: base((IRepository<Project>)repository, logger)
	{
		_user = user;
		_onboarding = onboarding;
		_bus = bus;
	}

	public async Task RefreshAsync()
	{
		_items = (await Repository.GetAll()).OrderBy(p => p.CreatedAt).ToList();
		if (_selectedProject is null || _items.All(p => p.Id != _selectedProject.Id))
			_selectedProject = _items.FirstOrDefault(p => p.IsDefault) ?? _items.FirstOrDefault();
		NotifyChanged();
	}

	private Task PublishChange()
	{
		if (!_user.IsAuthenticated) return Task.CompletedTask;
		return _bus.PublishAsync(new UserChangeEvent(_user.UserId, ChangeKind.Projects));
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
			await PublishChange();
		}
		return success;
	}

	public async Task DeleteProjectAsync(Project project)
	{
		await Repository.Delete(project);
		_items.RemoveAll(p => p.Id == project.Id);

		if (_selectedProject?.Id == project.Id)
			_selectedProject = _items.FirstOrDefault();

		NotifyChanged();
		await PublishChange();
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
