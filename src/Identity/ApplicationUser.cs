using Microsoft.AspNetCore.Identity;

namespace TodoList.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
	public string DisplayName { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
