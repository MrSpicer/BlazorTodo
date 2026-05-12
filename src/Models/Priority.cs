using System.ComponentModel.DataAnnotations;

namespace TodoList.Models;

public class Priority : IEntity
{
	public Guid Id { get; set; } = Guid.NewGuid();

	[Required(ErrorMessage = "Name is required")]
	[StringLength(40, MinimumLength = 1, ErrorMessage = "Name must be 1-40 characters")]
	public string Name { get; set; } = string.Empty;

	[StringLength(64, ErrorMessage = "Color value is too long")]
	public string Color { get; set; } = "#6366f1";

	public int Rank { get; set; }

	public bool IsBuiltIn { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.Now;
	public DateTime? UpdatedAt { get; set; }

	public bool IsValid() => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name);
}
