using TodoList.Models;

namespace TodoList.Services;

public interface IStatusService
{
	event Action? OnStatusesChanged;
	IReadOnlyList<Status> Statuses { get; }
	Task InitializeAsync();
	Status? GetById(Guid id);
	Task<bool> AddAsync(Status status);
	Task<bool> UpdateAsync(Status status);
	Task<int> GetUsageCountAsync(Guid statusId);
	Task<bool> DeleteAsync(Status status);
}
