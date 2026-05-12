using System.ComponentModel.DataAnnotations;

namespace TodoList.Models;

public class ProjectNote : IEntity, IProjectScoped
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid ProjectId { get; set; }

	[Required(ErrorMessage = "Title is required")]
	[StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be 1-200 characters")]
	public string Title { get; set; } = string.Empty;

	[StringLength(5000, ErrorMessage = "Content cannot exceed 5000 characters")]
	public string Content { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; } = DateTime.Now;

	public DateTime? UpdatedAt { get; set; }

	public DateTime? LastSyncedAt { get; set; }

	public bool IsDirty => LastSyncedAt is null || (UpdatedAt.HasValue && UpdatedAt > LastSyncedAt);

	public bool IsValid()
	{
		return Id != Guid.Empty && !string.IsNullOrWhiteSpace(Title) && ProjectId != Guid.Empty;
	}
}
