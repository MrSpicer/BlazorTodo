using System.ComponentModel.DataAnnotations;

namespace TodoList.Models;

public class Priority : IEntity
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid UserId { get; set; } = Guid.Empty;

	[Required(ErrorMessage = "Name is required")]
	[StringLength(40, MinimumLength = 1, ErrorMessage = "Name must be 1-40 characters")]
	public string Name { get; set; } = string.Empty;

	[StringLength(64, ErrorMessage = "Color value is too long")]
	[RegularExpression(@"^#[0-9a-fA-F]{3,8}$", ErrorMessage = "Color must be a valid hex color")]
	public string Color { get; set; } = "#6366f1";

	public int Rank { get; set; }

	public bool IsBuiltIn { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAt { get; set; }

	public bool IsValid() => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name);
}
