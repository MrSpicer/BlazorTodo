namespace TodoList.Services.Admin;

/// <summary>A single recorded failed sign-in attempt.</summary>
public sealed record LoginFailure(string Email, DateTime AtUtc);

/// <summary>
/// In-memory record of failed sign-in attempts for the admin dashboard. Counts accumulate from
/// app startup and are lost on restart (no persistence by design). A singleton written to by
/// <see cref="TodoList.Identity.TrackingSignInManager"/>.
/// </summary>
public interface ILoginActivityTracker
{
	/// <summary>Total failed sign-in attempts since the process started.</summary>
	int FailedSinceStartup { get; }

	/// <summary>When counting began (process start).</summary>
	DateTime StartedAtUtc { get; }

	/// <summary>The most recent failures (bounded), newest first.</summary>
	IReadOnlyList<LoginFailure> RecentFailures { get; }

	/// <summary>Records a failed sign-in attempt for the given email (may be unknown/null).</summary>
	void RecordFailure(string? email);
}
