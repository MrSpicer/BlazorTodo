namespace TodoList.Helpers;

/// <summary>
/// Formatting helpers for time durations.
/// </summary>
public static class Duration
{
	/// <summary>
	/// Formats a minute count as a compact human-readable span, e.g. 45 → "45m",
	/// 90 → "1h 30m", 120 → "2h".
	/// </summary>
	/// <param name="minutes">Whole minutes to render.</param>
	public static string FormatMinutes(int minutes)
	{
		if (minutes < 60) return $"{minutes}m";
		var h = minutes / 60;
		var m = minutes % 60;
		return m == 0 ? $"{h}h" : $"{h}h {m}m";
	}
}
