using System.ComponentModel.DataAnnotations;

namespace TodoList.Models;

public class Tag : IEntity
{
	public Guid Id { get; set; } = Guid.NewGuid();

	[Required(ErrorMessage = "Name is required")]
	[StringLength(40, MinimumLength = 1, ErrorMessage = "Name must be 1-40 characters")]
	public string Name { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; } = DateTime.Now;
	public DateTime? UpdatedAt { get; set; }

	public bool IsValid() => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name);
}
