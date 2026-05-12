using Blazored.LocalStorage;
using TodoList.Models;

namespace TodoList.Data;

public abstract class ProjectScopedRepository<T> : IProjectScopedRepository<T>
	where T : class, IEntity, IProjectScoped
{
	private readonly Dictionary<Guid, HashSet<Guid>> _idsByProject = new();
	protected readonly ILogger _logger;
	protected readonly ILocalStorageService _localStorage;

	protected abstract string StorageName { get; }
	private string ProjectIndexKey => $"{StorageName}_ProjectIds";
	private string LegacyIdsKey => $"{StorageName}_Ids";
	private string EntityKey(Guid id) => $"{StorageName}_{id}";
	private string BucketKey(Guid projectId) => $"{StorageName}_Project_{projectId}";

	protected ProjectScopedRepository(ILogger logger, ILocalStorageService localStorage)
	{
		_logger = logger;
		_localStorage = localStorage;
	}

	public virtual async Task InitializeAsync()
	{
		_idsByProject.Clear();

		var projectIndex = await _localStorage.GetItemAsync<HashSet<Guid>>(ProjectIndexKey);
		if (projectIndex is null)
		{
			if (await _localStorage.ContainKeyAsync(LegacyIdsKey))
				await MigrateLegacyAsync();
			return;
		}

		var bucketTasks = projectIndex.Select(async pid =>
			(pid, bucket: await _localStorage.GetItemAsync<HashSet<Guid>>(BucketKey(pid))));
		foreach (var (pid, bucket) in await Task.WhenAll(bucketTasks))
		{
			if (bucket is not null && bucket.Count > 0)
				_idsByProject[pid] = bucket;
		}
	}

	private async Task MigrateLegacyAsync()
	{
		var legacyIds = await _localStorage.GetItemAsync<HashSet<Guid>>(LegacyIdsKey);
		if (legacyIds is null || legacyIds.Count == 0)
		{
			await _localStorage.RemoveItemAsync(LegacyIdsKey);
			return;
		}

		_logger.LogInformation("Migrating {Count} legacy {Type} to per-project storage", legacyIds.Count, typeof(T).Name);

		foreach (var id in legacyIds)
		{
			var item = await _localStorage.GetItemAsync<T>(EntityKey(id));
			if (item is null)
				continue;
			if (!_idsByProject.TryGetValue(item.ProjectId, out var bucket))
			{
				bucket = new HashSet<Guid>();
				_idsByProject[item.ProjectId] = bucket;
			}
			bucket.Add(item.Id);
		}

		foreach (var (projectId, bucket) in _idsByProject)
			await _localStorage.SetItemAsync(BucketKey(projectId), bucket);
		await PersistProjectIndex();
		await _localStorage.RemoveItemAsync(LegacyIdsKey);
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
			var bucket = GetOrCreateBucket(entity.ProjectId, out var bucketCreated);
			var added = bucket.Add(entity.Id);

			await _localStorage.SetItemAsync(EntityKey(entity.Id), entity);

			if (added)
			{
				await _localStorage.SetItemAsync(BucketKey(entity.ProjectId), bucket);
				await RemoveFromOtherBuckets(entity.Id, entity.ProjectId);
			}
			if (bucketCreated)
				await PersistProjectIndex();

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
			if (!ContainsId(id))
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
			foreach (var bucket in _idsByProject.Values)
			{
				foreach (var id in bucket)
				{
					var item = await _localStorage.GetItemAsync<T>(EntityKey(id));
					if (item is not null)
						result.Add(item);
				}
			}
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving {Type} list", typeof(T).Name);
			return new List<T>();
		}
	}

	public virtual async Task<List<T>> GetByProject(Guid projectId)
	{
		try
		{
			var result = new List<T>();
			if (!_idsByProject.TryGetValue(projectId, out var bucket))
				return result;
			foreach (var id in bucket)
			{
				var item = await _localStorage.GetItemAsync<T>(EntityKey(id));
				if (item is not null)
					result.Add(item);
			}
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving {Type} for project {ProjectId}", typeof(T).Name, projectId);
			return new List<T>();
		}
	}

	public virtual async Task Delete(T entity)
	{
		if (entity is null)
			return;
		if (!_idsByProject.TryGetValue(entity.ProjectId, out var bucket) || !bucket.Remove(entity.Id))
			return;

		await _localStorage.RemoveItemAsync(EntityKey(entity.Id));

		if (bucket.Count == 0)
		{
			_idsByProject.Remove(entity.ProjectId);
			await _localStorage.RemoveItemAsync(BucketKey(entity.ProjectId));
			await PersistProjectIndex();
		}
		else
		{
			await _localStorage.SetItemAsync(BucketKey(entity.ProjectId), bucket);
		}
	}

	public virtual async Task DeleteByProject(Guid projectId)
	{
		if (!_idsByProject.TryGetValue(projectId, out var bucket))
			return;

		foreach (var id in bucket)
			await _localStorage.RemoveItemAsync(EntityKey(id));

		_idsByProject.Remove(projectId);
		await _localStorage.RemoveItemAsync(BucketKey(projectId));
		await PersistProjectIndex();
	}

	public virtual async Task ClearAll()
	{
		try
		{
			foreach (var (projectId, bucket) in _idsByProject)
			{
				foreach (var id in bucket)
					await _localStorage.RemoveItemAsync(EntityKey(id));
				await _localStorage.RemoveItemAsync(BucketKey(projectId));
			}
			_idsByProject.Clear();
			await _localStorage.RemoveItemAsync(ProjectIndexKey);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error clearing {Type}", typeof(T).Name);
		}
	}

	public async Task PersistToStorage()
	{
		foreach (var (projectId, bucket) in _idsByProject)
			await _localStorage.SetItemAsync(BucketKey(projectId), bucket);
		await PersistProjectIndex();
	}

	private HashSet<Guid> GetOrCreateBucket(Guid projectId, out bool created)
	{
		if (_idsByProject.TryGetValue(projectId, out var bucket))
		{
			created = false;
			return bucket;
		}
		bucket = new HashSet<Guid>();
		_idsByProject[projectId] = bucket;
		created = true;
		return bucket;
	}

	private bool ContainsId(Guid id)
	{
		foreach (var bucket in _idsByProject.Values)
		{
			if (bucket.Contains(id))
				return true;
		}
		return false;
	}

	private Task PersistProjectIndex() =>
		_localStorage.SetItemAsync(ProjectIndexKey, new HashSet<Guid>(_idsByProject.Keys)).AsTask();

	private async Task RemoveFromOtherBuckets(Guid entityId, Guid keepProjectId)
	{
		var emptiedProjects = new List<Guid>();
		var modifiedProjects = new List<Guid>();
		foreach (var (projectId, bucket) in _idsByProject)
		{
			if (projectId == keepProjectId)
				continue;
			if (bucket.Remove(entityId))
			{
				if (bucket.Count == 0)
					emptiedProjects.Add(projectId);
				else
					modifiedProjects.Add(projectId);
			}
		}

		foreach (var projectId in modifiedProjects)
			await _localStorage.SetItemAsync(BucketKey(projectId), _idsByProject[projectId]);

		foreach (var projectId in emptiedProjects)
		{
			_idsByProject.Remove(projectId);
			await _localStorage.RemoveItemAsync(BucketKey(projectId));
		}

		if (emptiedProjects.Count > 0)
			await PersistProjectIndex();
	}
}
