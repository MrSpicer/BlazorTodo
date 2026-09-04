using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using TodoList.Models.Enums;
using TodoList.Services.Access;

namespace TodoList.Helpers;

/// <summary>
/// Resolves the acting user's effective permissions in a single project. Anonymous/local users have
/// no sharing model, so they always get <see cref="ProjectPermission.All"/> — matching the gating in
/// Todo.razor and Kanban.razor.
/// </summary>
public static class PermissionResolver
{
	public static async Task<ProjectPermission> ForProjectAsync(
		Task<AuthenticationState>? authState,
		IProjectAccessResolver access,
		Guid projectId)
	{
		if (authState is null || projectId == Guid.Empty) return ProjectPermission.All;

		var state = await authState;
		if (state.User.Identity?.IsAuthenticated != true) return ProjectPermission.All;

		var sub = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (!Guid.TryParse(sub, out var userId) || userId == Guid.Empty) return ProjectPermission.All;

		return await access.GetEffectivePermissionsAsync(userId, projectId);
	}
}
