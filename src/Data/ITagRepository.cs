using TodoList.Models;

namespace TodoList.Data;

public interface ITagRepository
{
	Task<bool> AddOrUpdate(Tag tag);
	Task Delete(Tag tag);
	Task InitializeAsync();
	Task ClearAll();
	Task<List<Tag>> GetTags();
	Task<Tag?> Get(Guid id);
}
