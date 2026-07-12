namespace TodoList.Services.Admin;

/// <summary>
/// Tracks live Blazor Server circuits (connections) in-process for the admin dashboard.
/// State is held in memory only and resets when the app restarts. A singleton shared by all
/// circuits; the per-circuit <see cref="AdminCircuitHandler"/> adds/removes entries.
/// </summary>
public interface IConnectionTracker
{
	/// <summary>Total live circuits, authenticated or anonymous.</summary>
	int TotalConnections { get; }

	/// <summary>Live circuits belonging to a signed-in user.</summary>
	int AuthenticatedConnections { get; }

	/// <summary>Distinct signed-in users currently online (one user may hold several circuits).</summary>
	int DistinctUsersOnline { get; }

	/// <summary>Records a circuit as connected, optionally bound to an authenticated user.</summary>
	void Add(string circuitId, Guid? userId);

	/// <summary>Records a circuit as disconnected.</summary>
	void Remove(string circuitId);
}
