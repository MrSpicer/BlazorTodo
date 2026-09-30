using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoList.Models;

namespace TodoList.Data.Configurations;

public class ProjectNoteConfiguration : IEntityTypeConfiguration<ProjectNote>
{
	public void Configure(EntityTypeBuilder<ProjectNote> b)
	{
		b.ToTable("notes");
		b.HasKey(n => n.Id);
		b.Property(n => n.Id).HasColumnName("id");
		b.Property(n => n.UserId).HasColumnName("user_id");
		b.Property(n => n.ProjectId).HasColumnName("project_id");
		b.Property(n => n.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
		// No HasMaxLength -> Npgsql maps this to `text`, i.e. no length limit at all.
		b.Property(n => n.Content).HasColumnName("content");
		b.Property(n => n.CreatedAt).HasColumnName("created_at");
		b.Property(n => n.UpdatedAt).HasColumnName("updated_at");

		b.HasOne<Project>()
			.WithMany()
			.HasForeignKey(n => n.ProjectId)
			.OnDelete(DeleteBehavior.Cascade);

		b.HasIndex(n => new { n.UserId, n.ProjectId }).HasDatabaseName("ix_notes_user_id_project_id");
	}
}
