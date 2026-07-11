using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoList.Models;

namespace TodoList.Data.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
	public void Configure(EntityTypeBuilder<Tag> b)
	{
		b.ToTable("tags");
		b.HasKey(t => t.Id);
		b.Property(t => t.Id).HasColumnName("id");
		b.Property(t => t.UserId).HasColumnName("user_id");
		b.Property(t => t.Name).HasColumnName("name").HasMaxLength(40).IsRequired();
		b.Property(t => t.CreatedAt).HasColumnName("created_at");
		b.Property(t => t.UpdatedAt).HasColumnName("updated_at");

		b.HasIndex(t => new { t.UserId, t.Name })
			.IsUnique()
			.HasDatabaseName("ux_tags_user_id_name");
	}
}
