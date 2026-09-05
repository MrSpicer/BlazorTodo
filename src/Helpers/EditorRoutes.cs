using Microsoft.AspNetCore.Components;

namespace TodoList.Helpers;

/// <summary>
/// URL construction for the dedicated todo/note editor pages, plus return-URL validation.
/// Every caller that opens an editor goes through here so the query-string contract lives in one place.
/// </summary>
public static class EditorRoutes
{
	/// <summary>The current app-relative URL (path + query), suitable for a <c>returnUrl</c>.</summary>
	public static string Here(NavigationManager nav)
	{
		var p = nav.ToBaseRelativePath(nav.Uri);
		return p.StartsWith('/') ? p : "/" + p;
	}

	public static string NewTodo(string returnUrl, Guid? projectId = null, Guid? parentId = null) =>
		"/todos/new" + Query(
			("projectId", projectId?.ToString()),
			("parentId", parentId?.ToString()),
			("returnUrl", returnUrl));

	public static string EditTodo(Guid id, string returnUrl) =>
		$"/todos/{id}/edit" + Query(("returnUrl", returnUrl));

	public static string NewNote(string returnUrl, Guid? projectId = null) =>
		"/notes/new" + Query(
			("projectId", projectId?.ToString()),
			("returnUrl", returnUrl));

	public static string EditNote(Guid id, string returnUrl) =>
		$"/notes/{id}/edit" + Query(("returnUrl", returnUrl));

	/// <summary>
	/// Accepts a returnUrl only if it is an app-relative path. Rejects absolute URLs,
	/// protocol-relative (<c>//host</c>) and scheme-bearing (<c>javascript:</c>) values so a
	/// hand-crafted link can't turn an editor into an open redirect. Control characters and
	/// backslashes are rejected anywhere in the value, not just at the ends: browsers strip
	/// tab/CR/LF while parsing a URL, so <c>/&lt;LF&gt;/evil.com</c> would otherwise survive
	/// the prefix checks and then resolve to <c>//evil.com</c>.
	/// </summary>
	public static string SafeReturn(string? returnUrl, string fallback = "/")
	{
		if (string.IsNullOrWhiteSpace(returnUrl)) return fallback;
		var v = returnUrl.Trim();
		if (!v.StartsWith('/')) return fallback;
		if (v.Any(c => c < ' ' || c == '\u007f' || c == '\\')) return fallback;
		if (v.StartsWith("//")) return fallback;
		if (v.Contains(':')) return fallback;
		return v;
	}

	private static string Query(params (string Key, string? Value)[] parts)
	{
		var pairs = parts
			.Where(p => !string.IsNullOrWhiteSpace(p.Value))
			.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}")
			.ToArray();
		return pairs.Length == 0 ? string.Empty : "?" + string.Join("&", pairs);
	}
}
