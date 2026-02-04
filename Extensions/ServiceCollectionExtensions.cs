using TodoList.Data;
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
    public static IServiceCollection AddTodoServices(this IServiceCollection services)
    {
        services.AddScoped<ITodoRepository, TodoRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITodoService, TodoService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IDialogService, DialogService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IImportExportService, ImportExportService>();
        return services;
    }
}
