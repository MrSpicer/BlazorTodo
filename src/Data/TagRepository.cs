using Blazored.LocalStorage;
using TodoList.Models;

namespace TodoList.Data;

public class TagRepository : ITagRepository
{
	private HashSet<Guid> _tagIds = new();
	private readonly ILogger<TagRepository> _logger;
	private readonly ILocalStorageService _localStorage;

	private const string StorageName = "TagSet";

	public TagRepository(ILogger<TagRepository> logger, ILocalStorageService localStorage)
	{
		_logger = logger;
		_localStorage = localStorage;
	}

	async Task ITagRepository.InitializeAsync()
	{
		_tagIds = await _localStorage.GetItemAsync<HashSet<Guid>>($"{StorageName}_Ids") ?? new HashSet<Guid>();
	}

	async Task<bool> ITagRepository.AddOrUpdate(Tag tag)
	{
		if (tag is null || !tag.IsValid())
		{
			_logger.LogWarning("Malformed tag");
			return false;
		}

		try
		{
			if (!_tagIds.Contains(tag.Id))
			{
				_tagIds.Add(tag.Id);
				await PersistIds();
			}
			await _localStorage.SetItemAsync($"{StorageName}_{tag.Id}", tag);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating tag");
			return false;
		}
	}

	async Task<List<Tag>> ITagRepository.GetTags()
	{
		try
		{
			var set = new List<Tag>();
			foreach (var id in _tagIds)
			{
				var tag = await _localStorage.GetItemAsync<Tag>($"{StorageName}_{id}");
				if (tag != null)
					set.Add(tag);
			}
			return set;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving tags");
			return new List<Tag>();
		}
	}

	async Task<Tag?> ITagRepository.Get(Guid id)
	{
		try
		{
			if (!_tagIds.Contains(id))
				return null;
			return await _localStorage.GetItemAsync<Tag>($"{StorageName}_{id}");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving tag {Id}", id);
			return null;
		}
	}

	async Task ITagRepository.Delete(Tag tag)
	{
		if (_tagIds.Contains(tag.Id))
		{
			_tagIds.Remove(tag.Id);
			await PersistIds();
			await _localStorage.RemoveItemAsync($"{StorageName}_{tag.Id}");
		}
	}

	async Task ITagRepository.ClearAll()
	{
		foreach (var id in _tagIds)
		{
			await _localStorage.RemoveItemAsync($"{StorageName}_{id}");
		}
		await _localStorage.RemoveItemAsync($"{StorageName}_Ids");
		_tagIds.Clear();
	}

	private async Task PersistIds()
	{
		await _localStorage.SetItemAsync($"{StorageName}_Ids", _tagIds);
	}
}
