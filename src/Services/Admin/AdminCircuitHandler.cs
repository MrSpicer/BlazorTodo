using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace TodoList.Services.Admin;

/// <summary>
/// Scoped per-circuit handler that reports circuit lifecycle to the singleton
/// <see cref="IConnectionTracker"/>. Resolves the authenticated user (if any) from the
/// circuit's <see cref="AuthenticationStateProvider"/> — <c>IHttpContextAccessor</c> is not
/// usable inside a SignalR circuit, so the auth state provider is the correct source here.
/// </summary>
public sealed class AdminCircuitHandler : CircuitHandler
{
	private readonly IConnectionTracker _tracker;
	private readonly AuthenticationStateProvider _authStateProvider;
	private string? _circuitId;

	public AdminCircuitHandler(IConnectionTracker tracker, AuthenticationStateProvider authStateProvider)
	{
		_tracker = tracker;
		_authStateProvider = authStateProvider;
	}

	public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
	{
		_circuitId = circuit.Id;

		Guid? userId = null;
		var state = await _authStateProvider.GetAuthenticationStateAsync();
		var sub = state.User.FindFirstValue(ClaimTypes.NameIdentifier);
		if (Guid.TryParse(sub, out var id))
			userId = id;

		_tracker.Add(circuit.Id, userId);
	}

	public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
	{
		_tracker.Remove(circuit.Id);
		return Task.CompletedTask;
	}
}
