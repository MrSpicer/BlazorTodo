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
        services.AddScoped<ITodoService, TodoService>();
        services.AddScoped<IDialogService, DialogService>();
        return services;
    }
}
