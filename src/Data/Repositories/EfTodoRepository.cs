using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;
using TodoList.Models.Enums;

namespace TodoList.Data.Repositories;

// Todos are gated per action: reads require TodosRead, creating requires TodosAdd, editing
// requires TodosModify, deleting requires TodosRemove. The owner implicitly has all of them.

public class EfTodoRepository : EfRepositoryBase, ITodoRepository, IRepository<TodoItem>
{
	public EfTodoRepository(IDbContextFactory<AppDbContext> dbFactory, ICurrentUserContext user)
		: base(dbFactory, user) { }

	public Task InitializeAsync() => Task.CompletedTask;

	public Task PersistToStorage() => Task.CompletedTask;

	public Task<List<TodoItem>> GetAll() => GetTodos();

	public async Task<bool> AddOrUpdate(TodoItem todo)
	{
		if (todo is null || !todo.IsValid() || !PassesDataAnnotations(todo)) return false;
		var userId = RequireUserId();

		await using var db = await CreateDbAsync();
		// Hoisted so EF composes it as an `IN (subquery)`; calling the helper inside a lambda
		// would not translate. Membership-level scope for existing/parent lookups.
		var accessibleIds = ProjectAccessQueries.AccessibleProjectIds(db, userId);

		var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == todo.ProjectId);
		if (project is null) return false;

		var existing = await db.Todos.FirstOrDefaultAsync(t => t.Id == todo.Id &&
			accessibleIds.Contains(t.ProjectId));

		// Object-level authorization: creating needs TodosAdd, editing needs TodosModify.
		var required = existing is null ? ProjectPermission.TodosAdd : ProjectPermission.TodosModify;
		if (!await ProjectAccessQueries.HasPermissionAsync(db, userId, todo.ProjectId, required))
			return false;
		if (todo.ParentId is Guid parentId &&
			!await db.Todos.AnyAsync(t => t.Id == parentId && accessibleIds.Contains(t.ProjectId)))
			return false;

		// Partition key = the project owner (so shared todos resolve the owner's reference data).
		todo.UserId = project.UserId;

		// Assignee must be the owner or an accepted member of the project; otherwise clear it.
		if (todo.AssigneeId is Guid assigneeId &&
			assigneeId != project.UserId &&
			!await db.ProjectMembers.AnyAsync(m => m.ProjectId == todo.ProjectId &&
				m.UserId == assigneeId && m.Status == ProjectMemberStatus.Accepted))
		{
			todo.AssigneeId = null;
		}

		if (existing is null)
		{
			// New todo: default the owner to its creator (the acting user) when unset.
			if (todo.OwnerId == Guid.Empty) todo.OwnerId = userId;
			todo.UpdatedAt ??= DateTime.UtcNow;
			db.Todos.Add(todo);
		}
		else
		{
			// Preserve the original owner if the caller did not supply one.
			if (todo.OwnerId == Guid.Empty)
				todo.OwnerId = existing.OwnerId == Guid.Empty ? userId : existing.OwnerId;
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
		var accessibleIds = ProjectAccessQueries.AccessibleProjectIds(db, userId);
		var existing = await db.Todos
			.FirstOrDefaultAsync(t => t.Id == todo.Id && accessibleIds.Contains(t.ProjectId));
		if (existing is null) return;
		if (!await ProjectAccessQueries.HasPermissionAsync(db, userId, existing.ProjectId, ProjectPermission.TodosRemove))
			return;
		await db.Todos.Where(t => t.Id == existing.Id).ExecuteDeleteAsync();
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
		var readableIds = ProjectAccessQueries.ProjectIdsWith(db, userId, ProjectPermission.TodosRead);
		return await db.Todos
			.AsNoTracking()
			.Where(t => readableIds.Contains(t.ProjectId))
			.ToListAsync();
	}

	public async Task<List<TodoItem>> GetTodosByProject(Guid projectId)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		var readableIds = ProjectAccessQueries.ProjectIdsWith(db, userId, ProjectPermission.TodosRead);
		return await db.Todos
			.AsNoTracking()
			.Where(t => t.ProjectId == projectId && readableIds.Contains(t.ProjectId))
			.ToListAsync();
	}

	public async Task<TodoItem?> Get(Guid id)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		var readableIds = ProjectAccessQueries.ProjectIdsWith(db, userId, ProjectPermission.TodosRead);
		return await db.Todos
			.AsNoTracking()
			.FirstOrDefaultAsync(t => t.Id == id && readableIds.Contains(t.ProjectId));
	}

	public async Task DeleteByProject(Guid projectId)
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		// Only wipe a project's todos if the caller may remove todos there.
		if (!await ProjectAccessQueries.HasPermissionAsync(db, userId, projectId, ProjectPermission.TodosRemove)) return;
		await db.Todos.Where(t => t.ProjectId == projectId).ExecuteDeleteAsync();
	}
}
