using Blazored.LocalStorage;
using TodoList.Models;

namespace TodoList.Data;

public class StatusRepository : LocalStorageRepository<Status>, IStatusRepository
{
	protected override string StorageName => "StatusSet";

	public StatusRepository(ILogger<StatusRepository> logger, ILocalStorageService localStorage)
		: base(logger, localStorage)
	{
	}

	Task IStatusRepository.InitializeAsync() => InitializeAsync();
	Task<bool> IStatusRepository.AddOrUpdate(Status status) => AddOrUpdate(status);
	Task IStatusRepository.Delete(Status status) => Delete(status);
	Task<List<Status>> IStatusRepository.GetStatuses() => GetAll();
	Task<Status?> IStatusRepository.Get(Guid id) => Get(id);
	Task IStatusRepository.ClearAll() => ClearAll();
}
