using Blazored.LocalStorage;
using TodoList.Models;

namespace TodoList.Data;

public class ProjectRepository : LocalStorageRepository<Project>, IProjectRepository
{
	protected override string StorageName => "ProjectSet";

	public ProjectRepository(ILogger<ProjectRepository> logger, ILocalStorageService localStorage)
		: base(logger, localStorage)
	{
	}

	public async Task<List<Project>> GetProjects()
	{
		var all = await GetAll();
		return all.OrderBy(p => p.CreatedAt).ToList();
	}

	public Task<Project?> GetById(Guid id) => Get(id);
}
