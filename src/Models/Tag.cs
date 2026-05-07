using System.ComponentModel.DataAnnotations;

namespace TodoList.Models;

public class Tag
{
	public Guid Id { get; set; } = Guid.NewGuid();

	[Required(ErrorMessage = "Name is required")]
	[StringLength(40, MinimumLength = 1, ErrorMessage = "Name must be 1-40 characters")]
	public string Name { get; set; } = string.Empty;

	public bool IsValid() => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name);
}
