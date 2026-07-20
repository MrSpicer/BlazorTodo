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

	/// <summary>
	/// The anonymous visitor's session id from the <c>anon_sid</c> cookie, or <c>null</c> when the
	/// user is authenticated or no cookie is present. Only resolvable during an HTTP request
	/// (prerender); it is <c>null</c> once running inside a SignalR circuit.
	/// </summary>
	string? AnonymousSessionId { get; }
}
