namespace TodoList.Services.Admin;

/// <summary>
/// In-memory <see cref="ILoginActivityTracker"/>. Keeps a running failure count and a bounded
/// ring of the most recent failures. All mutation is under a lock — sign-in attempts arrive on
/// arbitrary request threads.
/// </summary>
public sealed class LoginActivityTracker : ILoginActivityTracker
{
	private const int MaxRecent = 50;

	private readonly object _gate = new();
	private readonly LinkedList<LoginFailure> _recent = new();
	private int _failed;

	public DateTime StartedAtUtc { get; } = DateTime.UtcNow;

	public int FailedSinceStartup
	{
		get { lock (_gate) { return _failed; } }
	}

	public IReadOnlyList<LoginFailure> RecentFailures
	{
		get { lock (_gate) { return _recent.ToList(); } }
	}

	public void RecordFailure(string? email)
	{
		var entry = new LoginFailure(
			string.IsNullOrWhiteSpace(email) ? "(unknown)" : email,
			DateTime.UtcNow);
		lock (_gate)
		{
			_failed++;
			_recent.AddFirst(entry);
			while (_recent.Count > MaxRecent)
				_recent.RemoveLast();
		}
	}
}
