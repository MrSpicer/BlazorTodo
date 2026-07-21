using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoList.Models;

namespace TodoList.Data.Configurations;

public class ProjectAccessRoleConfiguration : IEntityTypeConfiguration<ProjectAccessRole>
{
	public void Configure(EntityTypeBuilder<ProjectAccessRole> b)
	{
		b.ToTable("project_access_roles");
		b.HasKey(r => r.Id);
		b.Property(r => r.Id).HasColumnName("id");
		b.Property(r => r.ProjectId).HasColumnName("project_id");
		b.Property(r => r.RoleId).HasColumnName("role_id");
		// Stored as the enum's underlying int so bitwise permission checks translate to SQL.
		b.Property(r => r.Permissions).HasColumnName("permissions").HasConversion<int>();

		b.HasOne<Project>()
			.WithMany()
			.HasForeignKey(r => r.ProjectId)
			.OnDelete(DeleteBehavior.Cascade);

		b.HasOne<IdentityRole<Guid>>()
			.WithMany()
			.HasForeignKey(r => r.RoleId)
			.OnDelete(DeleteBehavior.Cascade);

		b.HasIndex(r => new { r.ProjectId, r.RoleId })
			.IsUnique()
			.HasDatabaseName("ux_project_access_roles_project_id_role_id");
	}
}
