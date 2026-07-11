using System.ComponentModel.DataAnnotations;

namespace TodoList.Models;

public class ProjectNote : IEntity, IProjectScoped
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid UserId { get; set; } = Guid.Empty;
	public Guid ProjectId { get; set; }

	[Required(ErrorMessage = "Title is required")]
	[StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be 1-200 characters")]
	public string Title { get; set; } = string.Empty;

	[StringLength(5000, ErrorMessage = "Content cannot exceed 5000 characters")]
	public string Content { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? UpdatedAt { get; set; }

	public bool IsValid()
	{
		return Id != Guid.Empty && !string.IsNullOrWhiteSpace(Title) && ProjectId != Guid.Empty;
	}
}
