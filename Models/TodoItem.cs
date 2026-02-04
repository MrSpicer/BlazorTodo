using System;
using System.ComponentModel.DataAnnotations;
using TodoList.Models.Enums;

namespace TodoList.Models;

public class TodoItem
{
	public Guid Id { get; set; } = Guid.NewGuid();

	[Required(ErrorMessage = "Title is required")]
	[StringLength(100, MinimumLength = 1, ErrorMessage = "Title must be 1-100 characters")]
	public string Title { get; set; } = string.Empty;

	[Required(ErrorMessage = "Description is required")]
	[StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
	public string Description { get; set; } = string.Empty;

	public Priority Priority { get; set; } = Priority.Medium;
	public TodoItemStatus Status { get; set; } = TodoItemStatus.None;
	public DateTime CreatedAt { get; set; } = DateTime.Now;
	public DateTime? StartedAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public Guid ProjectId { get; set; }

	public bool IsDone => Status == TodoItemStatus.Done;

	public bool IsValid()
	{
		return Id != Guid.Empty && !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Description);
	}
}
