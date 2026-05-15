namespace TodoList.Realtime;

public enum ChangeKind
{
	Todos,
	Projects,
	Notes,
}

public record UserChangeEvent(Guid UserId, ChangeKind Kind);

/// <summary>
/// In-process pub/sub for per-user change notifications, fan-out across Blazor circuits in
/// the same process. Each mutating service publishes after a successful commit; circuits owned
/// by the same user subscribe to refresh their in-memory caches.
///
/// Scaling note: for multi-node deployments, swap the implementation for a SignalR hub backed
/// by Redis. The publish/subscribe surface stays the same.
/// </summary>
public interface IUserChangeBus
{
	Task PublishAsync(UserChangeEvent ev);
	event Func<UserChangeEvent, Task>? OnChange;
}
