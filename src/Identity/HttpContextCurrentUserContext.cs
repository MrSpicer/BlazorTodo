using System.Security.Claims;

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
}
