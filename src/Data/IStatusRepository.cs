using TodoList.Models;

namespace TodoList.Data;

public interface IStatusRepository
{
	Task<bool> AddOrUpdate(Status status);
	Task Delete(Status status);
	Task InitializeAsync();
	Task ClearAll();
	Task<List<Status>> GetStatuses();
	Task<Status?> Get(Guid id);
}
