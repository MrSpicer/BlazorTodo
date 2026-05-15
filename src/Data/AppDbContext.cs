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
}
