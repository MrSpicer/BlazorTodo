using TodoList.Models;

namespace TodoList.Services;

/// <summary>
/// Service for managing projects.
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// Event raised when the project list changes.
    /// </summary>
    event Action? OnProjectsChanged;

    /// <summary>
    /// Gets the current list of projects.
    /// </summary>
    IReadOnlyList<Project> Projects { get; }

    /// <summary>
    /// Gets the currently selected project.
    /// </summary>
    Project? SelectedProject { get; }

    /// <summary>
    /// Initializes the service and loads projects from storage.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Saves a new or existing project.
    /// </summary>
    /// <param name="project">The project to save.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> SaveProjectAsync(Project project);

    /// <summary>
    /// Deletes a project.
    /// </summary>
    /// <param name="project">The project to delete.</param>
    Task DeleteProjectAsync(Project project);

    /// <summary>
    /// Selects a project as the current active project.
    /// </summary>
    /// <param name="project">The project to select.</param>
    void SelectProject(Project? project);

    /// <summary>
    /// Gets the default project.
    /// </summary>
    Project? GetDefaultProject();

    /// <summary>
    /// Gets the count of todos in a project.
    /// </summary>
    int GetTodoCount(Guid projectId, IReadOnlyList<TodoItem> todos);
}
