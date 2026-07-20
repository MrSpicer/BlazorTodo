using System.Security.Claims;
using TodoList.Services.Admin;

namespace TodoList.Identity;

public class HttpContextCurrentUserContext : ICurrentUserContext
{
	private readonly IHttpContextAccessor _httpContextAccessor;

	public HttpContextCurrentUserContext(IHttpContextAccessor httpContextAccessor)
	{
		_httpContextAccessor = httpContextAccessor;
	}

	public bool IsAuthenticated
		=> _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

	public Guid? UserIdOrNull
	{
		get
		{
			var sub = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
			return Guid.TryParse(sub, out var id) ? id : null;
		}
	}

	public Guid UserId
		=> UserIdOrNull ?? throw new InvalidOperationException("No authenticated user on the current request/circuit.");

	public string? AnonymousSessionId
	{
		get
		{
			var sid = _httpContextAccessor.HttpContext?.Request.Cookies[AnonymousSessionTracker.CookieName];
			return string.IsNullOrWhiteSpace(sid) ? null : sid;
		}
	}
}
