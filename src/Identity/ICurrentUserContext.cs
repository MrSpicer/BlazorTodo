namespace TodoList.Identity;

/// <summary>
/// Per-request/circuit user identity lookup. Use <see cref="UserIdOrNull"/> when callers
/// may be anonymous; use <see cref="UserId"/> when the caller has already established the
/// user must be authenticated (throws otherwise).
/// </summary>
public interface ICurrentUserContext
{
	bool IsAuthenticated { get; }
	Guid? UserIdOrNull { get; }
	Guid UserId { get; }
}
