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
    /// Gets the Bootstrap list-group-item class for a status id (built-ins only; custom statuses get neutral styling).
    /// </summary>
    public static string GetStatusItemClass(Guid statusId)
    {
        if (statusId == BuiltInStatusIds.Done) return "list-group-item-success";
        if (statusId == BuiltInStatusIds.Archived) return "list-group-item-light";
        if (statusId == BuiltInStatusIds.Abandoned) return "list-group-item-secondary";
        if (statusId == BuiltInStatusIds.InProgress) return "list-group-item-info";
        return "";
    }

    /// <summary>
    /// Gets the text decoration class for a status id.
    /// </summary>
    public static string GetStatusTextClass(Guid statusId)
    {
        if (statusId == BuiltInStatusIds.Done) return "text-decoration-line-through";
        if (statusId == BuiltInStatusIds.Archived || statusId == BuiltInStatusIds.Abandoned) return "text-muted";
        return "";
    }
}
