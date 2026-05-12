using Blazored.LocalStorage;
using TodoList.Models;

namespace TodoList.Data;

public abstract class LocalStorageRepository<T> : IRepository<T> where T : class, IEntity
{
	private readonly HashSet<Guid> _ids = new();
	protected readonly ILogger _logger;
	protected readonly ILocalStorageService _localStorage;

	protected abstract string StorageName { get; }
	private string IdsKey => $"{StorageName}_Ids";
	private string EntityKey(Guid id) => $"{StorageName}_{id}";

	protected IReadOnlyCollection<Guid> Ids => _ids;

	protected LocalStorageRepository(ILogger logger, ILocalStorageService localStorage)
	{
		_logger = logger;
		_localStorage = localStorage;
	}

	public virtual async Task InitializeAsync()
	{
		try
		{
			var ids = await _localStorage.GetItemAsync<HashSet<Guid>>(IdsKey);
			_ids.Clear();
			if (ids is not null)
			{
				foreach (var id in ids)
					_ids.Add(id);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error initializing {StorageName}", StorageName);
			_ids.Clear();
		}
	}

	public virtual async Task<bool> AddOrUpdate(T entity)
	{
		if (entity is null || !entity.IsValid())
		{
			_logger.LogWarning("Malformed {Type}", typeof(T).Name);
			return false;
		}

		try
		{
			if (_ids.Add(entity.Id))
				await PersistIds();
			await _localStorage.SetItemAsync(EntityKey(entity.Id), entity);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating {Type}", typeof(T).Name);
			return false;
		}
	}

	public virtual async Task<T?> Get(Guid id)
	{
		try
		{
			if (!_ids.Contains(id))
				return null;
			return await _localStorage.GetItemAsync<T>(EntityKey(id));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving {Type} {Id}", typeof(T).Name, id);
			return null;
		}
	}

	public virtual async Task<List<T>> GetAll()
	{
		try
		{
			var result = new List<T>();
			foreach (var id in _ids)
			{
				var item = await _localStorage.GetItemAsync<T>(EntityKey(id));
				if (item is not null)
					result.Add(item);
			}
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving {Type} list", typeof(T).Name);
			return new List<T>();
		}
	}

	public virtual async Task Delete(T entity)
	{
		if (entity is null)
			return;
		if (_ids.Remove(entity.Id))
		{
			await PersistIds();
			await _localStorage.RemoveItemAsync(EntityKey(entity.Id));
		}
	}

	public virtual async Task ClearAll()
	{
		try
		{
			foreach (var id in _ids)
				await _localStorage.RemoveItemAsync(EntityKey(id));
			await _localStorage.RemoveItemAsync(IdsKey);
			_ids.Clear();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error clearing {Type}", typeof(T).Name);
		}
	}

	private Task PersistIds() => _localStorage.SetItemAsync(IdsKey, _ids).AsTask();
}
