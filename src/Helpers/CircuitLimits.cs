namespace TodoList.Helpers;

/// <summary>
/// The one bound that free-text fields cannot escape. Note content and task descriptions are
/// uncapped in the domain model and in Postgres, but a textarea's whole value still crosses the
/// wire in a single SignalR hub message, and the hub caps how large a message it accepts. Exceeding
/// that cap kills the circuit ("connection lost") instead of failing validation, so the editors
/// guard against it with a <c>maxlength</c> derived from these numbers.
/// </summary>
public static class CircuitLimits
{
	/// <summary>
	/// Hub receive cap. The framework default is 32 KB; this is deliberately far higher so that
	/// uncapped text is carried rather than dropped. Dial this down to shrink the per-circuit
	/// receive buffer -- <see cref="MaxTextFieldChars"/> follows it automatically.
	/// </summary>
	public const int MaxReceiveMessageBytes = 4 * 1024 * 1024;

	/// <summary>
	/// How much of the cap a hub message's payload can actually occupy. Measured, not derived: with
	/// the cap at 4 MB a 2,000,000-character value arrives and 2,500,000 does not, so roughly half
	/// the configured cap is reachable -- the rest goes to framing Blazor adds around the value.
	/// </summary>
	private const int UsablePayloadBytes = MaxReceiveMessageBytes / 2;

	/// <summary>
	/// Worst case bytes one UTF-16 unit can cost on the wire. The browser JSON-encodes outbound
	/// messages as UTF-8, so ordinary text costs 1-3 bytes and emoji 2 -- but a control character
	/// becomes a \uXXXX escape, and Blazor nests the event args as a JSON string, which doubles the
	/// backslash: 7 bytes. Rounded to 8 to leave the estimate some margin.
	/// </summary>
	private const int WorstCaseBytesPerChar = 8;

	/// <summary>
	/// Largest text value that always fits one hub message, whatever it contains. Used as the
	/// textarea <c>maxlength</c> so overflow truncates in the browser instead of dropping the
	/// circuit. Ordinary prose could go several times further; this is the figure that holds even
	/// for pathological input.
	/// </summary>
	public const int MaxTextFieldChars = UsablePayloadBytes / WorstCaseBytesPerChar;
}
