namespace TodoList.Helpers;

/// <summary>
/// Clips long free text — descriptions and note bodies run to thousands of characters, and both
/// the list/kanban rows (CSS-clamped to two lines) and the change log only ever need the opening
/// of one. Clipping at the source keeps the render payload and the stored change log from scaling
/// with the full field length.
/// </summary>
public static class TextPreview
{
	/// <summary>
	/// Enough text to overflow the two-line CSS clamp on a todo row or kanban card at any
	/// width, so those views can stop sending the whole description to the browser.
	/// </summary>
	public const int ListPreviewLength = 200;

	/// <summary>
	/// Same idea for the three-line clamp on a note card, which needs more text to fill.
	/// </summary>
	public const int CardPreviewLength = 300;

	/// <summary>
	/// Returns <paramref name="text"/> unchanged when it fits in <paramref name="max"/> characters,
	/// otherwise the first <paramref name="max"/> characters followed by an ellipsis.
	/// </summary>
	public static string Clip(string? text, int max)
	{
		if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? string.Empty;
		// Never split a surrogate pair -- half an emoji renders as U+FFFD.
		var end = char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;
		return string.Concat(text.AsSpan(0, end), "…");
	}
}
