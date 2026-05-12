using TodoList.Data;
using TodoList.Models;

namespace TodoList.Services;

/// <summary>
/// Shared shell for entity services: holds the in-memory list, raises a change event,
/// and loads from the repository. Derived services keep their bespoke save/delete logic
/// and call <see cref="NotifyChanged"/> after mutating <see cref="_items"/>.
/// </summary>
public abstract class EntityServiceBase<T> where T : class, IEntity
{
	protected readonly IRepository<T> Repository;
	protected readonly ILogger Logger;
	protected List<T> _items = new();

	public event Action? OnChanged;
	public IReadOnlyList<T> Items => _items.AsReadOnly();

	protected EntityServiceBase(IRepository<T> repository, ILogger logger)
	{
		Repository = repository;
		Logger = logger;
	}

	public virtual async Task InitializeAsync()
	{
		await Repository.InitializeAsync();
		_items = await Repository.GetAll();
		NotifyChanged();
	}

	public virtual T? GetById(Guid id) => _items.FirstOrDefault(x => x.Id == id);

	protected void NotifyChanged() => OnChanged?.Invoke();
}
