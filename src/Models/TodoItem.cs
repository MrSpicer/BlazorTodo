using System;
using System.ComponentModel.DataAnnotations;
using TodoList.Models.Enums;

namespace TodoList.Models;

public class TodoItem : IEntity, IProjectScoped
{
	public Guid Id { get; set; } = Guid.NewGuid();

	[Required(ErrorMessage = "Title is required")]
	[StringLength(100, MinimumLength = 1, ErrorMessage = "Title must be 1-100 characters")]
	public string Title { get; set; } = string.Empty;

	[StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
	public string Description { get; set; } = string.Empty;

	/// <summary>Legacy fixed-set priority enum. Kept for one-time migration of pre-entity-Priority data; not authoritative — use <see cref="PriorityId"/>.</summary>
	public LegacyPriority Priority { get; set; } = LegacyPriority.Medium;

	public Guid PriorityId { get; set; } = Guid.Empty;

	/// <summary>Legacy enum status. Kept for one-time migration of pre-v1.3 data; not authoritative — use <see cref="StatusId"/>.</summary>
	public TodoItemStatus Status { get; set; } = TodoItemStatus.None;

	public Guid StatusId { get; set; } = Guid.Empty;

	public DateTime CreatedAt { get; set; } = DateTime.Now;
	public DateTime? StartedAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public DateTime? DueDate { get; set; }
	public int? EstimatedMinutes { get; set; }
	public DateTime? UpdatedAt { get; set; }
	public DateTime? LastSyncedAt { get; set; }
	public Guid ProjectId { get; set; }

	/// <summary>Null = top-level todo. Otherwise points at the parent <see cref="TodoItem"/>.</summary>
	public Guid? ParentId { get; set; }

	public List<Guid> TagIds { get; set; } = new();
	public List<TodoChangeLogEntry> ChangeLog { get; set; } = new();

	/// <summary>Legacy nested children. Pre-v1.5 data hydrates here; the one-time
	/// flatten in <c>TodoService</c> converts each entry into a top-level item with
	/// <see cref="ParentId"/> set and then clears this list.</summary>
	public List<TodoItem> SubTasks { get; set; } = new();

	public bool IsDone => StatusId == BuiltInStatusIds.Done;

	public bool IsDirty => LastSyncedAt is null || (UpdatedAt.HasValue && UpdatedAt > LastSyncedAt);
	public bool HasEverSynced => LastSyncedAt.HasValue;

	public bool IsValid()
	{
		return Id != Guid.Empty && !string.IsNullOrWhiteSpace(Title);
	}
}
