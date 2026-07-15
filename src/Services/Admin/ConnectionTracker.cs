using System.Collections.Concurrent;

namespace TodoList.Services.Admin;

/// <summary>
/// In-memory <see cref="IConnectionTracker"/>. Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// because circuit open/close callbacks fire on many threads concurrently.
/// </summary>
public sealed class ConnectionTracker : IConnectionTracker
{
	// circuit id -> authenticated user id (null for anonymous circuits).
	private readonly ConcurrentDictionary<string, Guid?> _circuits = new();

	public int TotalConnections => _circuits.Count;

	public int AuthenticatedConnections => _circuits.Count(c => c.Value is not null);

	public int DistinctUsersOnline =>
		_circuits.Values.Where(v => v is not null).Distinct().Count();

	public int UniqueConnections =>
		_circuits.Values.Count(v => v is null)                            // anonymous circuits, each unique
		+ _circuits.Values.Where(v => v is not null).Distinct().Count();  // one per signed-in user

	public void Add(string circuitId, Guid? userId) => _circuits[circuitId] = userId;

	public void Remove(string circuitId) => _circuits.TryRemove(circuitId, out _);

	public IReadOnlyDictionary<Guid, int> ConnectionsPerUser() =>
		_circuits.Values.Where(v => v is not null)
			.GroupBy(v => v!.Value)
			.ToDictionary(g => g.Key, g => g.Count());
}
