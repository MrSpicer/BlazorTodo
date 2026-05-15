using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

public class EfTodoRepository : EfRepositoryBase, ITodoRepository, IRepository<TodoItem>
{
	public EfTodoRepository(IDbContextFactory<AppDbContext> dbFactory, ICurrentUserContext user)
		: base(dbFactory, user) { }

	public Task InitializeAsync() => Task.CompletedTask;

	public Task PersistToStorage() => Task.CompletedTask;

	public Task<List<TodoItem>> GetAll() => GetTodos();

	public async Task<bool> AddOrUpdate(TodoItem todo)
	{
		if (todo is null || !todo.IsValid()) return false;
		var userId = RequireUserId();
		todo.UserId = userId;

		await using var db = await CreateDbAsync();
		var existing = await db.Todos.FirstOrDefaultAsync(t => t.Id == todo.Id && t.UserId == userId);
		if (existing is null)
		{
			todo.UpdatedAt ??= DateTime.UtcNow;
			db.Todos.Add(todo);
		}
		else
		{
			todo.UpdatedAt = DateTime.UtcNow;
			db.Entry(existing).CurrentValues.SetValues(todo);
		}
		await db.SaveChangesAsync();
		return true;
	}

	public async Task Delete(TodoItem todo)
	{
		if (todo is null) return;
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		await db.Todos.Where(t => t.Id == todo.Id && t.UserId == userId).ExecuteDeleteAsync();
	}

	public async Task ClearAll()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		await db.Todos.Where(t => t.UserId == userId).ExecuteDeleteAsync();
	}

	public async Task<List<TodoItem>> GetTodos()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await db.Todos
			.AsNoTracking()
			.Where(t => t.UserId == userId)
			.ToListAsync();
	}

	public async Task<List<TodoItem>> GetTodosByProject(Guid projectId)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await db.Todos
			.AsNoTracking()
			.Where(t => t.UserId == userId && t.ProjectId == projectId)
			.ToListAsync();
	}

	public async Task<TodoItem?> Get(Guid id)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await db.Todos
			.AsNoTracking()
			.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
	}

	public async Task DeleteByProject(Guid projectId)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		await db.Todos.Where(t => t.UserId == userId && t.ProjectId == projectId).ExecuteDeleteAsync();
	}
}
