using TodoList.Data;
using TodoList.Models;

namespace TodoList.Services;

public class ProjectService : EntityServiceBase<Project>, IProjectService
{
	private Project? _selectedProject;

	public event Action? OnProjectsChanged
	{
		add => OnChanged += value;
		remove => OnChanged -= value;
	}

	public IReadOnlyList<Project> Projects => Items;
	public Project? SelectedProject => _selectedProject;

	public ProjectService(IProjectRepository repository, ILogger<ProjectService> logger)
		: base((IRepository<Project>)repository, logger)
	{
	}

	public override async Task InitializeAsync()
	{
		await Repository.InitializeAsync();
		_items = (await Repository.GetAll()).OrderBy(p => p.CreatedAt).ToList();
		_selectedProject = _items.FirstOrDefault(p => p.IsDefault) ?? _items.FirstOrDefault();
		NotifyChanged();
	}

	public async Task<bool> SaveProjectAsync(Project project)
	{
		project.UpdatedAt = DateTime.Now;
		var success = await Repository.AddOrUpdate(project);
		if (success)
		{
			var idx = _items.FindIndex(p => p.Id == project.Id);
			if (idx >= 0)
				_items[idx] = project;
			else
				_items.Add(project);
			NotifyChanged();
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
