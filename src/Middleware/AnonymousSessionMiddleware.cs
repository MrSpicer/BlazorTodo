using System.Net;
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
		// count real page requests rather than the websocket handshake and static files. Also skip
		// health-check pings (see IsHealthCheck) so they don't pollute the admin report.
		var path = context.Request.Path.Value;
		if ((path is null || !path.StartsWith("/_", StringComparison.Ordinal))
			&& !IsHealthCheck(context))
		{
			tracker.RecordRequest(
				sessionId,
				context.Connection.RemoteIpAddress?.ToString(),
				context.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
		}

		await _next(context);
	}

	/// <summary>
	/// True for infrastructure health-check pings that should not show up in the admin dashboard.
	/// Both signals are required: the request comes from localhost (::1/127.0.0.1) <em>and</em>
	/// carries a probe user agent (the container health check uses curl; Wget is the other common
	/// probe) or none at all. Loopback alone is far too broad — under local <c>dotnet run</c> every
	/// browser request arrives from ::1, which would leave anonymous tracking permanently dead in
	/// development.
	/// </summary>
	private static bool IsHealthCheck(HttpContext context)
	{
		var ip = context.Connection.RemoteIpAddress;
		if (ip is null || !IPAddress.IsLoopback(ip))
		{
			return false;
		}

		var userAgent = context.Request.Headers.UserAgent.ToString();
		return userAgent.Length == 0
			|| userAgent.StartsWith("curl", StringComparison.OrdinalIgnoreCase)
			|| userAgent.StartsWith("Wget", StringComparison.OrdinalIgnoreCase);
	}
}
