using TodoList.Models;
using Blazored.LocalStorage;

namespace TodoList.Data;

public class NoteRepository : INoteRepository
{
	private readonly Dictionary<Guid, HashSet<Guid>> _idsByProject = new();
	private readonly ILogger<NoteRepository> _logger;
	private readonly ILocalStorageService _localStorage;

	private const string StorageName = "NoteSet";
	private const string LegacyIdsKey = "NoteSet_Ids";
	private const string ProjectIndexKey = "NoteSet_ProjectIds";

	public NoteRepository(ILogger<NoteRepository> logger, ILocalStorageService localStorage)
	{
		_logger = logger;
		_localStorage = localStorage;
	}

	public async Task InitializeAsync()
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

		foreach (var projectId in projectIndex)
		{
			var bucket = await _localStorage.GetItemAsync<HashSet<Guid>>(BucketKey(projectId));
			if (bucket is not null && bucket.Count > 0)
				_idsByProject[projectId] = bucket;
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

		_logger.LogInformation("Migrating {Count} legacy notes to per-project storage", legacyIds.Count);

		foreach (var id in legacyIds)
		{
			var item = await _localStorage.GetItemAsync<ProjectNote>(EntityKey(id));
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

	public async Task<bool> AddOrUpdate(ProjectNote note)
	{
		if (note == null || !note.IsValid())
		{
			_logger.LogWarning("Malformed note");
			return false;
		}

		try
		{
			var bucket = GetOrCreateBucket(note.ProjectId, out var bucketCreated);
			var added = bucket.Add(note.Id);

			await _localStorage.SetItemAsync(EntityKey(note.Id), note);

			if (added)
			{
				await _localStorage.SetItemAsync(BucketKey(note.ProjectId), bucket);
				await RemoveFromOtherBuckets(note.Id, note.ProjectId);
			}
			if (bucketCreated)
				await PersistProjectIndex();

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating note");
			return false;
		}
	}

	public async Task<List<ProjectNote>> GetNotes()
	{
		try
		{
			var notes = new List<ProjectNote>();
			foreach (var bucket in _idsByProject.Values)
			{
				foreach (var id in bucket)
				{
					var item = await _localStorage.GetItemAsync<ProjectNote>(EntityKey(id));
					if (item != null)
						notes.Add(item);
				}
			}
			return notes.OrderByDescending(n => n.CreatedAt).ToList();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving notes");
			return new List<ProjectNote>();
		}
	}

	public async Task<List<ProjectNote>> GetNotesByProject(Guid projectId)
	{
		try
		{
			var notes = new List<ProjectNote>();
			if (!_idsByProject.TryGetValue(projectId, out var bucket))
				return notes;
			foreach (var id in bucket)
			{
				var item = await _localStorage.GetItemAsync<ProjectNote>(EntityKey(id));
				if (item != null)
					notes.Add(item);
			}
			return notes.OrderByDescending(n => n.CreatedAt).ToList();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving notes for project {ProjectId}", projectId);
			return new List<ProjectNote>();
		}
	}

	public async Task Delete(ProjectNote note)
	{
		if (!_idsByProject.TryGetValue(note.ProjectId, out var bucket) || !bucket.Remove(note.Id))
			return;

		await _localStorage.RemoveItemAsync(EntityKey(note.Id));

		if (bucket.Count == 0)
		{
			_idsByProject.Remove(note.ProjectId);
			await _localStorage.RemoveItemAsync(BucketKey(note.ProjectId));
			await PersistProjectIndex();
		}
		else
		{
			await _localStorage.SetItemAsync(BucketKey(note.ProjectId), bucket);
		}
	}

	public async Task DeleteByProject(Guid projectId)
	{
		if (!_idsByProject.TryGetValue(projectId, out var bucket))
			return;

		foreach (var id in bucket)
			await _localStorage.RemoveItemAsync(EntityKey(id));

		_idsByProject.Remove(projectId);
		await _localStorage.RemoveItemAsync(BucketKey(projectId));
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

	private async Task PersistProjectIndex() =>
		await _localStorage.SetItemAsync(ProjectIndexKey, new HashSet<Guid>(_idsByProject.Keys));

	private async Task RemoveFromOtherBuckets(Guid noteId, Guid keepProjectId)
	{
		var emptiedProjects = new List<Guid>();
		var modifiedProjects = new List<Guid>();
		foreach (var (projectId, bucket) in _idsByProject)
		{
			if (projectId == keepProjectId)
				continue;
			if (bucket.Remove(noteId))
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
