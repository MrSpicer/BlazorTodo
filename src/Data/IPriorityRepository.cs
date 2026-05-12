using TodoList.Models;

namespace TodoList.Data;

public interface IPriorityRepository
{
	Task<bool> AddOrUpdate(Priority priority);
	Task Delete(Priority priority);
	Task InitializeAsync();
	Task ClearAll();
	Task<List<Priority>> GetPriorities();
	Task<Priority?> Get(Guid id);
}
