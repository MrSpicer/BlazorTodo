using System;
using TodoList.Models;
using Blazored.LocalStorage;
using System.Threading.Tasks;

namespace TodoList.Data;

public class TodoRepository : ITodoRepository
{
	private readonly Dictionary<Guid, HashSet<Guid>> _idsByProject = new();
	private readonly ILogger<TodoRepository> _logger;
	private readonly ILocalStorageService _localStorage;

	private const string StorageName = "TodoSet";
	private const string LegacyIdsKey = "TodoSet_Ids";
	private const string ProjectIndexKey = "TodoSet_ProjectIds";

	public TodoRepository(ILogger<TodoRepository> logger, ILocalStorageService localStorage)
	{
		_logger = logger;
		_localStorage = localStorage;
	}

	async Task ITodoRepository.InitializeAsync()
	{
		_idsByProject.Clear();

		var projectIndex = await _localStorage.GetItemAsync<HashSet<Guid>>(ProjectIndexKey);
		if (projectIndex is null)
		{
			if (await _localStorage.ContainKeyAsync(LegacyIdsKey))
			{
				await MigrateLegacyAsync();
			}
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

		_logger.LogInformation("Migrating {Count} legacy todos to per-project storage", legacyIds.Count);

		foreach (var id in legacyIds)
		{
			var item = await _localStorage.GetItemAsync<TodoItem>(EntityKey(id));
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

	async Task<bool> ITodoRepository.AddOrUpdate(TodoItem todo)
	{
		if (todo == null || !todo.IsValid())
		{
			_logger.LogWarning("Malformed todo");
			return false;
		}

		try
		{
			var bucket = GetOrCreateBucket(todo.ProjectId, out var bucketCreated);
			var added = bucket.Add(todo.Id);

			await _localStorage.SetItemAsync(EntityKey(todo.Id), todo);

			if (added)
			{
				await _localStorage.SetItemAsync(BucketKey(todo.ProjectId), bucket);
				await RemoveFromOtherBuckets(todo.Id, todo.ProjectId);
			}
			if (bucketCreated)
				await PersistProjectIndex();

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating todo");
			return false;
		}
	}

	async Task<TodoItem?> ITodoRepository.Get(Guid id)
	{
		try
		{
			if (!ContainsId(id))
				return null;
			return await _localStorage.GetItemAsync<TodoItem>(EntityKey(id));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving todo {Id}", id);
			return null;
		}
	}

	async Task<List<TodoItem>> ITodoRepository.GetTodos()
	{
		try
		{
			var todos = new List<TodoItem>();
			foreach (var bucket in _idsByProject.Values)
			{
				foreach (var id in bucket)
				{
					var item = await _localStorage.GetItemAsync<TodoItem>(EntityKey(id));
					if (item != null)
						todos.Add(item);
				}
			}
			return todos;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving todos");
			return new List<TodoItem>();
		}
	}

	async Task<List<TodoItem>> ITodoRepository.GetTodosByProject(Guid projectId)
	{
		try
		{
			var todos = new List<TodoItem>();
			if (!_idsByProject.TryGetValue(projectId, out var bucket))
				return todos;
			foreach (var id in bucket)
			{
				var item = await _localStorage.GetItemAsync<TodoItem>(EntityKey(id));
				if (item != null)
					todos.Add(item);
			}
			return todos;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving todos for project {ProjectId}", projectId);
			return new List<TodoItem>();
		}
	}

	async Task ITodoRepository.Delete(TodoItem todo)
	{
		if (!_idsByProject.TryGetValue(todo.ProjectId, out var bucket) || !bucket.Remove(todo.Id))
			return;

		await _localStorage.RemoveItemAsync(EntityKey(todo.Id));

		if (bucket.Count == 0)
		{
			_idsByProject.Remove(todo.ProjectId);
			await _localStorage.RemoveItemAsync(BucketKey(todo.ProjectId));
			await PersistProjectIndex();
		}
		else
		{
			await _localStorage.SetItemAsync(BucketKey(todo.ProjectId), bucket);
		}
	}

	async Task ITodoRepository.DeleteByProject(Guid projectId)
	{
		if (!_idsByProject.TryGetValue(projectId, out var bucket))
			return;

		foreach (var id in bucket)
			await _localStorage.RemoveItemAsync(EntityKey(id));

		_idsByProject.Remove(projectId);
		await _localStorage.RemoveItemAsync(BucketKey(projectId));
		await PersistProjectIndex();
	}

	public async Task PersistToStorage()
	{
		foreach (var (projectId, bucket) in _idsByProject)
			await _localStorage.SetItemAsync(BucketKey(projectId), bucket);
		await PersistProjectIndex();
	}

	public async Task ClearAll()
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

	private async Task PersistProjectIndex() =>
		await _localStorage.SetItemAsync(ProjectIndexKey, new HashSet<Guid>(_idsByProject.Keys));

	private async Task RemoveFromOtherBuckets(Guid todoId, Guid keepProjectId)
	{
		var emptiedProjects = new List<Guid>();
		var modifiedProjects = new List<Guid>();
		foreach (var (projectId, bucket) in _idsByProject)
		{
			if (projectId == keepProjectId)
				continue;
			if (bucket.Remove(todoId))
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

	private static string EntityKey(Guid id) => $"{StorageName}_{id}";
	private static string BucketKey(Guid projectId) => $"{StorageName}_Project_{projectId}";
}
