using TodoList.Services.Admin;

namespace TodoList.Middleware;

/// <summary>
/// For unauthenticated visitors, ensures a stable <c>anon_sid</c> cookie and records their
/// activity into <see cref="IAnonymousSessionTracker"/> for the admin dashboard. Must run after
/// <c>UseAuthentication</c> (so <see cref="HttpContext.User"/> is populated) and on the HTTP page
/// response — a cookie cannot be set over the live SignalR circuit.
/// </summary>
public sealed class AnonymousSessionMiddleware
{
	private readonly RequestDelegate _next;

	public AnonymousSessionMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context, IAnonymousSessionTracker tracker)
	{
		// Authenticated users are tracked by user id elsewhere; leave any existing cookie alone.
		if (context.User.Identity?.IsAuthenticated == true)
		{
			await _next(context);
			return;
		}

		var sessionId = context.Request.Cookies[AnonymousSessionTracker.CookieName];
		if (string.IsNullOrWhiteSpace(sessionId))
		{
			sessionId = Guid.NewGuid().ToString("N");
			context.Response.Cookies.Append(AnonymousSessionTracker.CookieName, sessionId, new CookieOptions
			{
				HttpOnly = true,
				SameSite = SameSiteMode.Lax,
				IsEssential = true,
				Path = "/",
				MaxAge = TimeSpan.FromDays(365),
				// Secure whenever the (forwarded) request is HTTPS: always in prod behind the
				// Cloudflare HTTPS hop, relaxed for local plain-HTTP dev so the cookie is still sent.
				Secure = context.Request.IsHttps,
			});
		}

		// Skip framework/asset traffic (the /_blazor negotiate, /_framework, /_content, etc.) so we
		// count real page requests rather than the websocket handshake and static files.
		var path = context.Request.Path.Value;
		if (path is null || !path.StartsWith("/_", StringComparison.Ordinal))
		{
			tracker.RecordRequest(
				sessionId,
				context.Connection.RemoteIpAddress?.ToString(),
				context.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
		}

		await _next(context);
	}
}
