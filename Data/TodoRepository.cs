using System;
using TodoList.Models;
using Blazored.LocalStorage;
using TodoList.Pages;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

namespace TodoList.Data;

public class TodoRepository : ITodoRepository
{
	private HashSet<Guid> TodoIds = new();
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
		TodoIds = await _localStorage.GetItemAsync<HashSet<Guid>>($"{_storageName}_Ids") ?? new HashSet<Guid>();
	}

	async Task<bool> ITodoRepository.AddOrUpdate(TodoItem todo)
	{
		if (todo == null || !todo.IsValid())
		{
			_logger.Log(LogLevel.Debug, $"Malformed todo");
			return false;
		}


		try
		{
			if (!TodoIds.Contains(todo.Id))
			{
				TodoIds.Add(todo.Id);
				await this.PersistToStorage();
			}
			await _localStorage.SetItemAsync($"{_storageName}_{todo.Id}", todo);
			return true;
		}
		catch (Exception ex)
		{
			_logger.Log(LogLevel.Error, $"Error updating todo: {ex.Message}");
			return false;
		}
	}

	async Task<List<TodoItem>> ITodoRepository.GetTodos()
	{
		try
		{
			var set = new List<TodoItem>();
			foreach (Guid id in TodoIds)
			{
				var item = await _localStorage.GetItemAsync<TodoItem>($"{_storageName}_{id}");
				if (item != null)
				{
					set.Add(item);
				}
			}
			return set;
		}
		catch (Exception ex)
		{
			_logger.Log(LogLevel.Error, $"Error retrieving todos: {ex.Message}");
			return new List<TodoItem>();
		}
	}

	async Task ITodoRepository.Delete(TodoItem todo)
	{
		if (TodoIds.Contains(todo.Id))
		{
			TodoIds.Remove(todo.Id);
			await this.PersistToStorage();
			await _localStorage.RemoveItemAsync($"{_storageName}_{todo.Id}");
		}
	}

	public async Task PersistToStorage()
	{
		await _localStorage.SetItemAsync($"{_storageName}_Ids", TodoIds);
	}
}