using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Configurations;

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
	public void Configure(EntityTypeBuilder<ProjectMember> b)
	{
		b.ToTable("project_members");
		b.HasKey(m => m.Id);
		b.Property(m => m.Id).HasColumnName("id");
		b.Property(m => m.ProjectId).HasColumnName("project_id");
		b.Property(m => m.UserId).HasColumnName("user_id");
		b.Property(m => m.RoleId).HasColumnName("role_id");
		b.Property(m => m.Status).HasColumnName("status").HasConversion<int>();
		b.Property(m => m.InviteToken).HasColumnName("invite_token").HasMaxLength(128);
		b.Property(m => m.InvitedByUserId).HasColumnName("invited_by_user_id");
		b.Property(m => m.CanManageAccess).HasColumnName("can_manage_access");
		b.Property(m => m.CreatedAt).HasColumnName("created_at");
		b.Property(m => m.UpdatedAt).HasColumnName("updated_at");
		b.Property(m => m.AcceptedAt).HasColumnName("accepted_at");

		b.HasOne<Project>()
			.WithMany()
			.HasForeignKey(m => m.ProjectId)
			.OnDelete(DeleteBehavior.Cascade);

		b.HasOne<ApplicationUser>()
			.WithMany()
			.HasForeignKey(m => m.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		b.HasOne<IdentityRole<Guid>>()
			.WithMany()
			.HasForeignKey(m => m.RoleId)
			.OnDelete(DeleteBehavior.Restrict);

		b.HasIndex(m => new { m.ProjectId, m.UserId })
			.IsUnique()
			.HasDatabaseName("ux_project_members_project_id_user_id");
		b.HasIndex(m => new { m.UserId, m.Status }).HasDatabaseName("ix_project_members_user_id_status");
		b.HasIndex(m => m.InviteToken).HasDatabaseName("ix_project_members_invite_token");
	}
}
