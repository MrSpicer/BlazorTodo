using System;
using TodoList.Models;
using Blazored.LocalStorage;
using TodoList.Pages;

namespace TodoList.Data;

public class TodoRepository : ITodoRepository, IDisposable
{
	private HashSet<TodoItem> TodoSet = new();
	private ILogger<TodoRepository> _logger;
	private ILocalStorageService _localStorage;

	private string _storageName = "TodoSet";

	public TodoRepository(ILogger<TodoRepository> logger, ILocalStorageService LocalStorage)
	{
		_logger = logger;
		_localStorage = LocalStorage;
	}
	async Task ITodoRepository.InitializeAsync()
	{
		TodoSet = await _localStorage.GetItemAsync<HashSet<TodoItem>>(_storageName) ?? new HashSet<TodoItem>();
	}

	bool ITodoRepository.AddOrUpdate(TodoItem todo)
	{
		if (todo == null || !todo.IsValid())
		{
			_logger.Log(LogLevel.Debug, $"Malformed todo");
			return false;
		}

		if (TodoSet.Contains(todo) == true)
		{
			_logger.Log(LogLevel.Debug, $"Todo Already up to date: {todo.Title}");
			return false;
		}


		try
		{
			if (TodoSet.Any(t => t.Id == todo.Id))
			{
				TodoSet.RemoveWhere(t => t.Id == todo.Id);
			}

			TodoSet.Add(todo);
			this.PersistToStorage();
			return true;
		}
		catch (Exception ex)
		{
			_logger.Log(LogLevel.Error, $"Error updating todo: {ex.Message}");
			return false;
		}
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
			this.PersistToStorage();
		}
	}

	public void PersistToStorage()
	{
		_localStorage.SetItemAsync(_storageName, TodoSet);
	}

	public void Dispose()
	{
		this.PersistToStorage();
	}
}