using System;

namespace TodoList.Models;

public class TodoItem
{
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Priority Priority { get; set; }

	public bool IsDone { get; set; }

	public bool IsValid()
	{
		return !String.IsNullOrWhiteSpace(Title);
	}
}

public enum Priority { Low, Medium, High, Emergency }
