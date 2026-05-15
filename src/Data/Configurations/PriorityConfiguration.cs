using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoList.Models;

namespace TodoList.Data.Configurations;

public class PriorityConfiguration : IEntityTypeConfiguration<Priority>
{
	public void Configure(EntityTypeBuilder<Priority> b)
	{
		b.ToTable("priorities");
		b.HasKey(p => p.Id);
		b.Property(p => p.Id).HasColumnName("id");
		b.Property(p => p.UserId).HasColumnName("user_id");
		b.Property(p => p.Name).HasColumnName("name").HasMaxLength(40).IsRequired();
		b.Property(p => p.Color).HasColumnName("color").HasMaxLength(64);
		b.Property(p => p.Rank).HasColumnName("rank");
		b.Property(p => p.IsBuiltIn).HasColumnName("is_built_in");
		b.Property(p => p.CreatedAt).HasColumnName("created_at");
		b.Property(p => p.UpdatedAt).HasColumnName("updated_at");

		b.HasIndex(p => new { p.UserId, p.Name })
			.IsUnique()
			.HasDatabaseName("ux_priorities_user_id_name");
	}
}
