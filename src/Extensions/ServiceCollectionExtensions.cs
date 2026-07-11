using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using TodoList.Data;
using TodoList.Data.Repositories;
using TodoList.Identity;
using TodoList.Realtime;
using TodoList.Services;

namespace TodoList.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to register application services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all todo-related services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddTodoServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is required. Set it in appsettings.Development.json for local dev, " +
                "or via environment variable in Swarm/production.");

        // Blazor Server circuits live longer than a single request — sharing one scoped
        // DbContext across the circuit causes "Connection is not open" errors. Register the
        // factory (singleton) and a scoped DbContext that's created fresh per scope from the
        // factory; Identity uses the scoped one, our EF repos use the factory directly so
        // each call gets a fresh short-lived context.
        //
        // EnableDynamicJson is required by Npgsql 8+ for jsonb columns mapped to generic
        // List<T> / Dictionary types (e.g. TodoItem.ChangeLog, FilterPreset.SortCriteria).
        var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(dataSource));
        services.AddScoped<AppDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                // RequireConfirmedAccount gates the Identity UI Register page's post-create
                // branch: when true it redirects to RegisterConfirmation without auto-signin.
                // RequireConfirmedEmail gates subsequent password sign-ins via
                // SignInManager.CanSignInAsync. Both are needed — different jobs.
                options.SignIn.RequireConfirmedAccount = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequiredLength = 10;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultUI()
            .AddDefaultTokenProviders();

        services.AddTransient<IEmailSender, SmtpEmailSender>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, HttpContextCurrentUserContext>();
        services.AddScoped<IUserOnboardingService, UserOnboardingService>();

        // In-process pub/sub for multi-device change notifications. Singleton because all
        // circuits (regardless of user) share the same bus and filter events by UserId.
        services.AddSingleton<IUserChangeBus, UserChangeBus>();

        // LocalStorage repositories (anonymous-user path).
        services.AddScoped<TodoRepository>();
        services.AddScoped<ProjectRepository>();
        services.AddScoped<NoteRepository>();
        services.AddScoped<TagRepository>();
        services.AddScoped<StatusRepository>();
        services.AddScoped<PriorityRepository>();
        services.AddScoped<FilterPresetRepository>();

        // EF Core repositories (authenticated-user path).
        services.AddScoped<EfTodoRepository>();
        services.AddScoped<EfProjectRepository>();
        services.AddScoped<EfNoteRepository>();
        services.AddScoped<EfTagRepository>();
        services.AddScoped<EfStatusRepository>();
        services.AddScoped<EfPriorityRepository>();
        services.AddScoped<EfFilterPresetRepository>();

        // Routing wrappers — dispatch by ICurrentUserContext.IsAuthenticated.
        services.AddScoped<ITodoRepository, RoutingTodoRepository>();
        services.AddScoped<IProjectRepository, RoutingProjectRepository>();
        services.AddScoped<INoteRepository, RoutingNoteRepository>();
        services.AddScoped<ITagRepository, RoutingTagRepository>();
        services.AddScoped<IStatusRepository, RoutingStatusRepository>();
        services.AddScoped<IPriorityRepository, RoutingPriorityRepository>();
        services.AddScoped<IFilterPresetRepository, RoutingFilterPresetRepository>();

        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IStatusService, StatusService>();
        services.AddScoped<IPriorityService, PriorityService>();
        services.AddScoped<IChangeLogFormatter, ChangeLogFormatter>();
        services.AddScoped<ITodoService, TodoService>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IDialogService, DialogService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<AppDataSerializer>();
        services.AddScoped<IImportExportService, ImportExportService>();
        services.AddScoped<IFilterPresetService, FilterPresetService>();
        return services;
    }
}
