using Blazored.LocalStorage;
using TodoList.Models;

namespace TodoList.Data;

public class TagRepository : LocalStorageRepository<Tag>, ITagRepository
{
	protected override string StorageName => "TagSet";

	public TagRepository(ILogger<TagRepository> logger, ILocalStorageService localStorage)
		: base(logger, localStorage)
	{
	}

	Task ITagRepository.InitializeAsync() => InitializeAsync();
	Task<bool> ITagRepository.AddOrUpdate(Tag tag) => AddOrUpdate(tag);
	Task ITagRepository.Delete(Tag tag) => Delete(tag);
	Task<List<Tag>> ITagRepository.GetTags() => GetAll();
	Task<Tag?> ITagRepository.Get(Guid id) => Get(id);
	Task ITagRepository.ClearAll() => ClearAll();
}
