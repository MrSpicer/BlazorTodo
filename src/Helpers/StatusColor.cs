using System.Globalization;

namespace TodoList.Helpers;

public static class StatusColor
{
	public const string Default = "#6366f1";
	public const string Fallback = "#f3f4f6";

	private static readonly Dictionary<string, string> _legacyTokenToHex = new(StringComparer.OrdinalIgnoreCase)
	{
		["status-info"]    = "#dbeafe",
		["status-warning"] = "#fef3c7",
		["status-success"] = "#d1fae5",
		["status-danger"]  = "#fee2e2",
		["status-primary"] = "#eef2ff",
		["status-muted"]   = "#e5e7eb",
		["status-light"]   = "#f3f4f6",
		["status-dark"]    = "#1f2937",
	};

	public static (string Background, string Foreground) Resolve(string? color)
	{
		var bg = NormalizeBackground(color);
		return (bg, ComputeForeground(bg));
	}

	public static string NormalizeBackground(string? color)
	{
		if (string.IsNullOrWhiteSpace(color))
			return Fallback;

		var trimmed = color.Trim();
		if (IsHex(trimmed))
			return ExpandHex(trimmed);

		if (_legacyTokenToHex.TryGetValue(trimmed, out var mapped))
			return mapped;

		return Fallback;
	}

	public static string ComputeForeground(string backgroundHex)
	{
		if (!TryParseRgb(backgroundHex, out var r, out var g, out var b))
			return "#1f2937";

		double Linear(double c)
		{
			c /= 255.0;
			return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
		}

		var luminance = 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
		return luminance > 0.5 ? "#1f2937" : "#ffffff";
	}

	public static bool IsHex(string value)
	{
		if (string.IsNullOrEmpty(value) || value[0] != '#')
			return false;

		var hex = value.AsSpan(1);
		if (hex.Length != 3 && hex.Length != 6)
			return false;

		foreach (var ch in hex)
		{
			if (!Uri.IsHexDigit(ch))
				return false;
		}
		return true;
	}

	private static string ExpandHex(string value)
	{
		if (value.Length == 7)
			return value.ToLowerInvariant();

		// #rgb → #rrggbb
		var r = value[1];
		var g = value[2];
		var b = value[3];
		return $"#{r}{r}{g}{g}{b}{b}".ToLowerInvariant();
	}

	private static bool TryParseRgb(string hex, out int r, out int g, out int b)
	{
		r = g = b = 0;
		if (!IsHex(hex))
			return false;

		var expanded = ExpandHex(hex);
		return int.TryParse(expanded.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
		    && int.TryParse(expanded.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
		    && int.TryParse(expanded.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
	}
}
