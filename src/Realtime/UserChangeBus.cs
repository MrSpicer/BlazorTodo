namespace TodoList.Realtime;

public class UserChangeBus : IUserChangeBus
{
	private readonly ILogger<UserChangeBus> _logger;

	public UserChangeBus(ILogger<UserChangeBus> logger)
	{
		_logger = logger;
	}

	public event Func<UserChangeEvent, Task>? OnChange;

	public async Task PublishAsync(UserChangeEvent ev)
	{
		var handlers = OnChange?.GetInvocationList();
		if (handlers is null) return;

		foreach (var d in handlers)
		{
			try
			{
				if (d is Func<UserChangeEvent, Task> handler)
					await handler(ev);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UserChangeBus subscriber threw for event {Event}", ev);
			}
		}
	}
}
