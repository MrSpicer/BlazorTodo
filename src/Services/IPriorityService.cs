using TodoList.Models;

namespace TodoList.Services;

public interface IPriorityService
{
	event Action? OnPrioritiesChanged;
	IReadOnlyList<Priority> Priorities { get; }
	Task InitializeAsync();
	Priority? GetById(Guid id);
	Task<bool> AddAsync(Priority priority);
	Task<bool> UpdateAsync(Priority priority);
	Task<int> GetUsageCountAsync(Guid priorityId);
	Task<bool> DeleteAsync(Priority priority);
}
