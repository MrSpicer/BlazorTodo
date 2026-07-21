using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
	public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
	{
	}

	public DbSet<Project> Projects => Set<Project>();
	public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
	public DbSet<ProjectAccessRole> ProjectAccessRoles => Set<ProjectAccessRole>();
	public DbSet<TodoItem> Todos => Set<TodoItem>();
	public DbSet<ProjectNote> Notes => Set<ProjectNote>();
	public DbSet<Tag> Tags => Set<Tag>();
	public DbSet<Status> Statuses => Set<Status>();
	public DbSet<Priority> Priorities => Set<Priority>();
	public DbSet<FilterPreset> FilterPresets => Set<FilterPreset>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
	}

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		base.ConfigureConventions(configurationBuilder);

		// timestamptz columns only accept Kind=Utc; see UtcDateTimeConverter.
		configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
		configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
	}
}
