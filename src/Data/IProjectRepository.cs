using TodoList.Models;

namespace TodoList.Data;

public interface IProjectRepository
{
    Task<bool> AddOrUpdate(Project project);
    Task Delete(Project project);
    Task InitializeAsync();
    Task<List<Project>> GetProjects();
    Task<Project?> GetById(Guid id);
}
