using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoList.Models;

namespace TodoList.Data.Configurations;

public class StatusConfiguration : IEntityTypeConfiguration<Status>
{
	public void Configure(EntityTypeBuilder<Status> b)
	{
		b.ToTable("statuses");
		b.HasKey(s => s.Id);
		b.Property(s => s.Id).HasColumnName("id");
		b.Property(s => s.UserId).HasColumnName("user_id");
		b.Property(s => s.Name).HasColumnName("name").HasMaxLength(40).IsRequired();
		b.Property(s => s.Color).HasColumnName("color").HasMaxLength(64);
		b.Property(s => s.IsBuiltIn).HasColumnName("is_built_in");
		b.Property(s => s.CreatedAt).HasColumnName("created_at");
		b.Property(s => s.UpdatedAt).HasColumnName("updated_at");

		b.HasIndex(s => new { s.UserId, s.Name })
			.IsUnique()
			.HasDatabaseName("ux_statuses_user_id_name");
	}
}
