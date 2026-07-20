namespace TodoList.Services.Admin;

/// <summary>
/// In-memory <see cref="IAnonymousSessionTracker"/>. All mutation is under a single lock —
/// activity arrives on arbitrary request threads and circuit open/close on others. Bounded to
/// <see cref="MaxSessions"/> entries; when full, the oldest <em>offline</em> session is evicted.
/// </summary>
public sealed class AnonymousSessionTracker : IAnonymousSessionTracker
{
	/// <summary>Name of the cookie holding the anonymous session id. Shared with the middleware
	/// and <c>ICurrentUserContext</c> so all three agree on the key.</summary>
	public const string CookieName = "anon_sid";

	private const int MaxSessions = 500;

	private readonly object _gate = new();
	private readonly Dictionary<string, AnonymousSessionInfo> _sessions = new();
	// connection key (per live circuit) -> session id, so MarkOffline can find its session.
	private readonly Dictionary<string, string> _connections = new();

	public int TotalSessions
	{
		get { lock (_gate) { return _sessions.Count; } }
	}

	public int OnlineSessions
	{
		get { lock (_gate) { return _sessions.Values.Count(s => s.LiveConnections > 0); } }
	}

	public void RecordRequest(string sessionId, string? ip, string? userAgent)
	{
		if (string.IsNullOrWhiteSpace(sessionId))
			return;

		var now = DateTime.UtcNow;
		lock (_gate)
		{
			if (_sessions.TryGetValue(sessionId, out var info))
			{
				info.LastSeenUtc = now;
				info.RequestCount++;
				info.LastIp = ip;
				info.LastUserAgent = userAgent;
			}
			else
			{
				_sessions[sessionId] = new AnonymousSessionInfo
				{
					SessionId = sessionId,
					FirstSeenUtc = now,
					LastSeenUtc = now,
					RequestCount = 1,
					LastIp = ip,
					LastUserAgent = userAgent,
				};
				EvictIfNeeded();
			}
		}
	}

	public void MarkOnline(string sessionId, string connectionKey)
	{
		if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(connectionKey))
			return;

		var now = DateTime.UtcNow;
		lock (_gate)
		{
			if (!_connections.TryAdd(connectionKey, sessionId))
				return; // already registered

			if (_sessions.TryGetValue(sessionId, out var info))
			{
				info.LiveConnections++;
				info.LastSeenUtc = now;
			}
			else
			{
				// A circuit can outlive the request that recorded the session (e.g. eviction);
				// recreate a minimal entry so presence is still visible.
				_sessions[sessionId] = new AnonymousSessionInfo
				{
					SessionId = sessionId,
					FirstSeenUtc = now,
					LastSeenUtc = now,
					RequestCount = 0,
					LiveConnections = 1,
				};
				EvictIfNeeded();
			}
		}
	}

	public void MarkOffline(string connectionKey)
	{
		if (string.IsNullOrWhiteSpace(connectionKey))
			return;

		lock (_gate)
		{
			if (!_connections.Remove(connectionKey, out var sessionId))
				return;

			if (_sessions.TryGetValue(sessionId, out var info) && info.LiveConnections > 0)
				info.LiveConnections--;
		}
	}

	public IReadOnlyList<AnonymousSessionInfo> Sessions()
	{
		lock (_gate)
		{
			// Return copies so callers can't mutate live state outside the lock.
			return _sessions.Values
				.OrderByDescending(s => s.LastSeenUtc)
				.Select(s => new AnonymousSessionInfo
				{
					SessionId = s.SessionId,
					FirstSeenUtc = s.FirstSeenUtc,
					LastSeenUtc = s.LastSeenUtc,
					RequestCount = s.RequestCount,
					LastIp = s.LastIp,
					LastUserAgent = s.LastUserAgent,
					LiveConnections = s.LiveConnections,
				})
				.ToList();
		}
	}

	// Caller must hold _gate. Drops the least-recently-seen offline session to stay bounded.
	private void EvictIfNeeded()
	{
		if (_sessions.Count <= MaxSessions)
			return;

		var victim = _sessions.Values
			.Where(s => s.LiveConnections == 0)
			.OrderBy(s => s.LastSeenUtc)
			.FirstOrDefault();

		if (victim is not null)
			_sessions.Remove(victim.SessionId);
	}
}
