using TodoList.Models;

namespace TodoList.Services;

public interface ITagService
{
	event Action? OnTagsChanged;
	IReadOnlyList<Tag> Tags { get; }
	Task InitializeAsync();
	Task<Tag> GetOrCreateAsync(string name);
	Tag? GetById(Guid id);
	IEnumerable<Tag> Search(string query);
	Task<bool> UpdateAsync(Tag tag);
	Task<int> GetUsageCountAsync(Guid tagId);
	Task DeleteAsync(Tag tag);
}
