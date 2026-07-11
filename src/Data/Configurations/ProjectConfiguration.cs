using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoList.Models;

namespace TodoList.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
	public void Configure(EntityTypeBuilder<Project> b)
	{
		b.ToTable("projects");
		b.HasKey(p => p.Id);
		b.Property(p => p.Id).HasColumnName("id");
		b.Property(p => p.UserId).HasColumnName("user_id");
		b.Property(p => p.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
		b.Property(p => p.Description).HasColumnName("description").HasMaxLength(200);
		b.Property(p => p.Color).HasColumnName("color").HasMaxLength(9);
		b.Property(p => p.CreatedAt).HasColumnName("created_at");
		b.Property(p => p.UpdatedAt).HasColumnName("updated_at");
		b.Property(p => p.IsDefault).HasColumnName("is_default");

		b.HasIndex(p => p.UserId).HasDatabaseName("ix_projects_user_id");
	}
}
