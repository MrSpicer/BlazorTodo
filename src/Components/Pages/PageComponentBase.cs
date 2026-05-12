using Microsoft.AspNetCore.Components;

namespace TodoList.Components.Pages;

/// <summary>
/// Base class for pages that subscribe to service change events and need IDisposable cleanup.
/// Derived pages call <see cref="Track"/> to register an unsubscribe action and override
/// <see cref="OnFirstRenderAsync"/> to run service initialization once after the first render.
/// </summary>
public abstract class PageComponentBase : ComponentBase, IDisposable
{
	private readonly List<Action> _disposers = new();
	private bool _disposed;

	/// <summary>
	/// Register an action to run when this component is disposed. Typically used to
	/// pair an event subscription with its teardown:
	/// <code>
	/// Service.OnChanged += StateHasChanged;
	/// Track(() =&gt; Service.OnChanged -= StateHasChanged);
	/// </code>
	/// </summary>
	protected void Track(Action onDispose) => _disposers.Add(onDispose);

	/// <summary>
	/// Override to run async service initialization on first render. The base
	/// triggers <c>StateHasChanged</c> afterward so the page rebinds against loaded data.
	/// </summary>
	protected virtual Task OnFirstRenderAsync() => Task.CompletedTask;

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
			await OnFirstRenderAsync();
			await InvokeAsync(StateHasChanged);
		}
	}

	public virtual void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		foreach (var d in _disposers)
		{
			try { d(); }
			catch { }
		}
		_disposers.Clear();
	}
}
