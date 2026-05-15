using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoList.Models;

namespace TodoList.Data.Configurations;

public class FilterPresetConfiguration : IEntityTypeConfiguration<FilterPreset>
{
	public void Configure(EntityTypeBuilder<FilterPreset> b)
	{
		b.ToTable("filter_presets");
		b.HasKey(p => p.Id);
		b.Property(p => p.Id).HasColumnName("id");
		b.Property(p => p.UserId).HasColumnName("user_id");
		b.Property(p => p.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
		b.Property(p => p.SearchText).HasColumnName("search_text").HasMaxLength(500);

		b.Property(p => p.SelectedPriorities)
			.HasColumnName("selected_priorities")
			.HasColumnType("jsonb");

		b.Property(p => p.SelectedStatuses)
			.HasColumnName("selected_statuses")
			.HasColumnType("jsonb");

		b.Property(p => p.SortCriteria)
			.HasColumnName("sort_criteria")
			.HasColumnType("jsonb");

		b.HasIndex(p => new { p.UserId, p.Name })
			.IsUnique()
			.HasDatabaseName("ux_filter_presets_user_id_name");
	}
}
