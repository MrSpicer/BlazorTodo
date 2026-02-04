using System;
using TodoList.Models;
using Blazored.LocalStorage;

namespace TodoList.Data;

public class ProjectRepository : IProjectRepository
{
    private HashSet<Guid> ProjectIds = new();
    private readonly ILogger<ProjectRepository> _logger;
    private readonly ILocalStorageService _localStorage;
    private const string StorageName = "ProjectSet";

    public ProjectRepository(ILogger<ProjectRepository> logger, ILocalStorageService localStorage)
    {
        _logger = logger;
        _localStorage = localStorage;
    }

    public async Task InitializeAsync()
    {
        ProjectIds = await _localStorage.GetItemAsync<HashSet<Guid>>($"{StorageName}_Ids") ?? new HashSet<Guid>();
    }

    public async Task<bool> AddOrUpdate(Project project)
    {
        if (project == null || !project.IsValid())
        {
            _logger.Log(LogLevel.Debug, "Malformed project");
            return false;
        }

        try
        {
            if (!ProjectIds.Contains(project.Id))
            {
                ProjectIds.Add(project.Id);
                await PersistToStorage();
            }
            await _localStorage.SetItemAsync($"{StorageName}_{project.Id}", project);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Error updating project: {ex.Message}");
            return false;
        }
    }

    public async Task<List<Project>> GetProjects()
    {
        try
        {
            var projects = new List<Project>();
            foreach (Guid id in ProjectIds)
            {
                var item = await _localStorage.GetItemAsync<Project>($"{StorageName}_{id}");
                if (item != null)
                {
                    projects.Add(item);
                }
            }
            return projects.OrderBy(p => p.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Error retrieving projects: {ex.Message}");
            return new List<Project>();
        }
    }

    public async Task<Project?> GetById(Guid id)
    {
        try
        {
            return await _localStorage.GetItemAsync<Project>($"{StorageName}_{id}");
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Error retrieving project: {ex.Message}");
            return null;
        }
    }

    public async Task Delete(Project project)
    {
        if (ProjectIds.Contains(project.Id))
        {
            ProjectIds.Remove(project.Id);
            await PersistToStorage();
            await _localStorage.RemoveItemAsync($"{StorageName}_{project.Id}");
        }
    }

    private async Task PersistToStorage()
    {
        await _localStorage.SetItemAsync($"{StorageName}_Ids", ProjectIds);
    }
}
