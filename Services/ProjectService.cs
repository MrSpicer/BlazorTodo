using TodoList.Data;
using TodoList.Models;

namespace TodoList.Services;

/// <summary>
/// Implementation of IProjectService for managing projects.
/// </summary>
public class ProjectService : IProjectService
{
    private readonly IProjectRepository _repository;
    private List<Project> _projects = new();
    private Project? _selectedProject;

    public event Action? OnProjectsChanged;
    public IReadOnlyList<Project> Projects => _projects.AsReadOnly();
    public Project? SelectedProject => _selectedProject;

    public ProjectService(IProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task InitializeAsync()
    {
        await _repository.InitializeAsync();
        _projects = await _repository.GetProjects();
        
        // Select the default project initially
        _selectedProject = _projects.FirstOrDefault(p => p.IsDefault) ?? _projects.FirstOrDefault();
        
        NotifyStateChanged();
    }

    public async Task<bool> SaveProjectAsync(Project project)
    {
        var success = await _repository.AddOrUpdate(project);
        if (success)
        {
            _projects = await _repository.GetProjects();
            NotifyStateChanged();
        }
        return success;
    }

    public async Task DeleteProjectAsync(Project project)
    {
        await _repository.Delete(project);
        _projects = await _repository.GetProjects();
        
        // If we deleted the selected project, select the first remaining one
        if (_selectedProject?.Id == project.Id)
        {
            _selectedProject = _projects.FirstOrDefault();
        }
        
        NotifyStateChanged();
    }

    public void SelectProject(Project? project)
    {
        _selectedProject = project;
        NotifyStateChanged();
    }

    public Project? GetDefaultProject()
    {
        return _projects.FirstOrDefault(p => p.IsDefault);
    }

    public int GetTodoCount(Guid projectId, IReadOnlyList<TodoItem> todos)
    {
        return todos.Count(t => t.ProjectId == projectId);
    }

    private void NotifyStateChanged() => OnProjectsChanged?.Invoke();
}
