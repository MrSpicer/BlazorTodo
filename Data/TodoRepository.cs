using System;
using TodoList.Models;

namespace TodoList.Data;

public class TodoRepository : ITodoRepository
{
	private static HashSet<TodoItem> TodoSet = new();
	private ILogger<TodoRepository> _logger;

	public TodoRepository(ILogger<TodoRepository> logger)
	{
		_logger = logger;
	}

	bool ITodoRepository.Add(TodoItem todo)
	{
		if (todo == null || !todo.IsValid())
		{
			_logger.Log(LogLevel.Debug, $"Malformed todo");
			return false;
		}

		TodoSet.Add(todo);
		_logger.Log(LogLevel.Trace, $"Todo Added: {todo.Title}");
		return true;
	}

	IQueryable<TodoItem> ITodoRepository.GetTodos()
	{
		_logger.Log(LogLevel.Trace, $"Todos retrieved");
		return TodoSet.AsQueryable();
	}

	void ITodoRepository.Delete(TodoItem todo)
	{
		if (TodoSet.Contains(todo))
		{
			_logger.Log(LogLevel.Trace, $"Todo Deleted: {todo.Title}");
			TodoSet.Remove(todo);
		}
	}
}