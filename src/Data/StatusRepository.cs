using Blazored.LocalStorage;
using TodoList.Models;

namespace TodoList.Data;

public class StatusRepository : IStatusRepository
{
	private HashSet<Guid> _statusIds = new();
	private readonly ILogger<StatusRepository> _logger;
	private readonly ILocalStorageService _localStorage;

	private const string StorageName = "StatusSet";

	public StatusRepository(ILogger<StatusRepository> logger, ILocalStorageService localStorage)
	{
		_logger = logger;
		_localStorage = localStorage;
	}

	async Task IStatusRepository.InitializeAsync()
	{
		_statusIds = await _localStorage.GetItemAsync<HashSet<Guid>>($"{StorageName}_Ids") ?? new HashSet<Guid>();
	}

	async Task<bool> IStatusRepository.AddOrUpdate(Status status)
	{
		if (status is null || !status.IsValid())
		{
			_logger.LogWarning("Malformed status");
			return false;
		}

		try
		{
			if (!_statusIds.Contains(status.Id))
			{
				_statusIds.Add(status.Id);
				await PersistIds();
			}
			await _localStorage.SetItemAsync($"{StorageName}_{status.Id}", status);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating status");
			return false;
		}
	}

	async Task<List<Status>> IStatusRepository.GetStatuses()
	{
		try
		{
			var set = new List<Status>();
			foreach (var id in _statusIds)
			{
				var status = await _localStorage.GetItemAsync<Status>($"{StorageName}_{id}");
				if (status != null)
					set.Add(status);
			}
			return set;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving statuses");
			return new List<Status>();
		}
	}

	async Task<Status?> IStatusRepository.Get(Guid id)
	{
		try
		{
			if (!_statusIds.Contains(id))
				return null;
			return await _localStorage.GetItemAsync<Status>($"{StorageName}_{id}");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving status {Id}", id);
			return null;
		}
	}

	async Task IStatusRepository.Delete(Status status)
	{
		if (_statusIds.Contains(status.Id))
		{
			_statusIds.Remove(status.Id);
			await PersistIds();
			await _localStorage.RemoveItemAsync($"{StorageName}_{status.Id}");
		}
	}

	async Task IStatusRepository.ClearAll()
	{
		foreach (var id in _statusIds)
		{
			await _localStorage.RemoveItemAsync($"{StorageName}_{id}");
		}
		await _localStorage.RemoveItemAsync($"{StorageName}_Ids");
		_statusIds.Clear();
	}

	private async Task PersistIds()
	{
		await _localStorage.SetItemAsync($"{StorageName}_Ids", _statusIds);
	}
}
