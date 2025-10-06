using System;
using TodoList.Pages;

namespace TodoList.Models;

public class TodoItem
{
	public Guid Id { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Priority Priority { get; set; }
	public TodoItemStatus Status { get; set; } = TodoItemStatus.None;
	public DateTime CreatedAt { get; set; } = DateTime.Now;
	public DateTime? StartedAt { get; set; }

	public bool IsDone
	{
		get { return Status == TodoItemStatus.Done; }
		set
		{
			if (value)
			{
				Status = TodoItemStatus.Done;
			}
		}
	}

	public bool IsValid()
	{
		return Id != Guid.Empty && !String.IsNullOrWhiteSpace(Title) && !String.IsNullOrWhiteSpace(Description);
	}
}

public enum Priority { Low, Medium, High, Emergency }
public enum TodoItemStatus { None, New, InProgress, Done, Abandoned, Archived }
