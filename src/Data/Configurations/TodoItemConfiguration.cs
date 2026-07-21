using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoList.Models;

namespace TodoList.Data.Configurations;

public class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
	public void Configure(EntityTypeBuilder<TodoItem> b)
	{
		b.ToTable("todos");
		b.HasKey(t => t.Id);
		b.Property(t => t.Id).HasColumnName("id");
		b.Property(t => t.UserId).HasColumnName("user_id");
		b.Property(t => t.OwnerId).HasColumnName("owner_id");
		b.Property(t => t.AssigneeId).HasColumnName("assignee_id");
		b.Property(t => t.ProjectId).HasColumnName("project_id");
		b.Property(t => t.ParentId).HasColumnName("parent_id");
		b.Property(t => t.Title).HasColumnName("title").HasMaxLength(100).IsRequired();
		b.Property(t => t.Description).HasColumnName("description").HasMaxLength(500);
		b.Property(t => t.PriorityId).HasColumnName("priority_id");
		b.Property(t => t.StatusId).HasColumnName("status_id");
		b.Property(t => t.CreatedAt).HasColumnName("created_at");
		b.Property(t => t.StartedAt).HasColumnName("started_at");
		b.Property(t => t.CompletedAt).HasColumnName("completed_at");
		b.Property(t => t.DueDate).HasColumnName("due_date");
		b.Property(t => t.EstimatedMinutes).HasColumnName("estimated_minutes");
		b.Property(t => t.UpdatedAt).HasColumnName("updated_at");

		b.Property(t => t.TagIds)
			.HasColumnName("tag_ids")
			.HasColumnType("uuid[]");

		b.Property(t => t.ChangeLog)
			.HasColumnName("change_log")
			.HasColumnType("jsonb");

		b.HasOne<Project>()
			.WithMany()
			.HasForeignKey(t => t.ProjectId)
			.OnDelete(DeleteBehavior.Cascade);

		b.HasOne<TodoItem>()
			.WithMany()
			.HasForeignKey(t => t.ParentId)
			.OnDelete(DeleteBehavior.Cascade);

		b.HasIndex(t => new { t.UserId, t.ProjectId }).HasDatabaseName("ix_todos_user_id_project_id");
		b.HasIndex(t => t.ParentId).HasDatabaseName("ix_todos_parent_id");
		b.HasIndex(t => t.AssigneeId).HasDatabaseName("ix_todos_assignee_id");
	}
}
