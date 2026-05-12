using TodoList.Models;

namespace TodoList.Data;

public interface IRepository<T> where T : class, IEntity
{
	Task InitializeAsync();
	Task<bool> AddOrUpdate(T entity);
	Task Delete(T entity);
	Task<T?> Get(Guid id);
	Task<List<T>> GetAll();
	Task ClearAll();
}

public interface IProjectScopedRepository<T> : IRepository<T> where T : class, IEntity, IProjectScoped
{
	Task<List<T>> GetByProject(Guid projectId);
	Task DeleteByProject(Guid projectId);
}
