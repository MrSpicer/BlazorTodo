namespace TodoList.Services.Admin;

/// <summary>
/// A single tracked anonymous visitor, identified by the <c>anon_sid</c> cookie value.
/// Fields are mutated under the tracker's lock; treat instances returned from
/// <see cref="IAnonymousSessionTracker.Sessions"/> as immutable snapshots.
/// </summary>
public sealed class AnonymousSessionInfo
{
	public string SessionId { get; init; } = string.Empty;
	public DateTime FirstSeenUtc { get; init; }
	public DateTime LastSeenUtc { get; set; }
	public int RequestCount { get; set; }
	public string? LastIp { get; set; }
	public string? LastUserAgent { get; set; }

	/// <summary>Number of live Blazor circuits for this session; &gt; 0 means "online now".</summary>
	public int LiveConnections { get; set; }
}

/// <summary>
/// Tracks anonymous (unauthenticated) visitors for the admin dashboard, keyed by the
/// <see cref="AnonymousSessionTracker.CookieName"/> cookie. State is held in memory only and
/// resets when the app restarts — a singleton shared across all requests and circuits, mirroring
/// <see cref="IConnectionTracker"/> / <see cref="ILoginActivityTracker"/>. The
/// <c>AnonymousSessionMiddleware</c> feeds request activity; the <c>AnonymousPresence</c>
/// component feeds live-circuit presence.
/// </summary>
public interface IAnonymousSessionTracker
{
	/// <summary>Records HTTP activity for a session: creates it on first sight, else bumps the
	/// request count and refreshes last-seen / ip / user-agent.</summary>
	void RecordRequest(string sessionId, string? ip, string? userAgent);

	/// <summary>Marks a live circuit connected for a session. <paramref name="connectionKey"/> is a
	/// per-circuit token used to reverse the effect in <see cref="MarkOffline"/>.</summary>
	void MarkOnline(string sessionId, string connectionKey);

	/// <summary>Marks a previously registered circuit disconnected.</summary>
	void MarkOffline(string connectionKey);

	/// <summary>Snapshot of all tracked sessions, most-recently-seen first.</summary>
	IReadOnlyList<AnonymousSessionInfo> Sessions();

	/// <summary>Total tracked sessions.</summary>
	int TotalSessions { get; }

	/// <summary>Sessions with at least one live circuit.</summary>
	int OnlineSessions { get; }
}
