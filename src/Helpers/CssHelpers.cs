using TodoList.Models;
using TodoList.Models.Enums;

namespace TodoList.Helpers;

/// <summary>
/// Helper methods for generating CSS classes based on todo item properties.
/// </summary>
public static class CssHelpers
{
    /// <summary>
    /// Gets the Bootstrap badge class for a priority level.
    /// </summary>
    public static string GetPriorityBadgeClass(Priority priority) => priority switch
    {
        Priority.Low => "bg-secondary",
        Priority.Medium => "bg-primary",
        Priority.High => "bg-warning text-dark",
        Priority.Emergency => "bg-danger",
        _ => "bg-secondary"
    };

    /// <summary>
    /// Gets the Bootstrap list-group-item class for a status.
    /// </summary>
    public static string GetStatusItemClass(TodoItemStatus status) => status switch
    {
        TodoItemStatus.Done => "list-group-item-success",
        TodoItemStatus.Archived => "list-group-item-light",
        TodoItemStatus.Abandoned => "list-group-item-secondary",
        TodoItemStatus.InProgress => "list-group-item-info",
        _ => ""
    };

    /// <summary>
    /// Gets the text decoration class for a status.
    /// </summary>
    public static string GetStatusTextClass(TodoItemStatus status) => status switch
    {
        TodoItemStatus.Done => "text-decoration-line-through",
        TodoItemStatus.Archived or TodoItemStatus.Abandoned => "text-muted",
        _ => ""
    };
}
