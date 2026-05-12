using Blazored.LocalStorage;
using TodoList.Models;

namespace TodoList.Data;

public class PriorityRepository : LocalStorageRepository<Priority>, IPriorityRepository
{
	protected override string StorageName => "PrioritySet";

	public PriorityRepository(ILogger<PriorityRepository> logger, ILocalStorageService localStorage)
		: base(logger, localStorage)
	{
	}

	Task IPriorityRepository.InitializeAsync() => InitializeAsync();
	Task<bool> IPriorityRepository.AddOrUpdate(Priority priority) => AddOrUpdate(priority);
	Task IPriorityRepository.Delete(Priority priority) => Delete(priority);
	Task<List<Priority>> IPriorityRepository.GetPriorities() => GetAll();
	Task<Priority?> IPriorityRepository.Get(Guid id) => Get(id);
	Task IPriorityRepository.ClearAll() => ClearAll();
}
