namespace Cirreum.Components.Pages;

using Microsoft.AspNetCore.Components;

/// <summary>
/// A base page that provides strongly-typed sync state management integration.
/// Extends <see cref="StatePageBase"/> with automatic injection of a specific
/// <typeparamref name="TState"/> sync state type.
/// </summary>
/// <typeparam name="TState">
/// The type of state to inject. Must implement <see cref="IApplicationState"/>.
/// </typeparam>
/// <remarks>
/// <para>This generic base class provides:</para>
/// <list type="bullet">
///     <item>
///         <description>
///         Automatic injection of <typeparamref name="TState"/> as the <see cref="State"/> property.
///         </description>
///     </item>
///     <item>
///         <description>
///         Virtual <see cref="OnStateChanged"/> called when the injected state changes
///         via <c>NotifySubscribers</c>.
///         </description>
///     </item>
///     <item>
///         <description>
///         All services from <see cref="StatePageBase"/> (Logger, CurrentUser, JSApp,
///         Mediatr, Dialogr, Toastr, state subscriptions).
///         </description>
///     </item>
/// </list>
/// <para>
/// Use this base class when you want strongly-typed access to a specific state interface.
/// For built-in state types, consider <see cref="SessionStatePage"/>,
/// <see cref="LocalStatePage"/>, or <see cref="MemoryStatePage"/>.
/// </para>
/// </remarks>
public abstract class StatePageBase<TState> : StatePageBase
	where TState : IApplicationState {

	/// <summary>
	/// The injected state instance of type <typeparamref name="TState"/>.
	/// </summary>
	[Inject]
	protected TState State { get; set; } = default!;

	private bool _stateSubscribed;

	// -------------------------------------------------------------------------
	// Override Hook
	// -------------------------------------------------------------------------

	/// <summary>
	/// Called when the injected state has changed via <c>NotifySubscribers</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Override in derived classes to react to external state changes — from other
	/// components, background services, or external events.
	/// </para>
	/// <para>
	/// Note: This is only called for external state changes. UI interactions within
	/// this component trigger re-rendering directly without calling this method.
	/// </para>
	/// </remarks>
	protected virtual void OnStateChanged() { }

	// -------------------------------------------------------------------------
	// Lifecycle
	// -------------------------------------------------------------------------

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// Automatically subscribes to sync state changes for <typeparamref name="TState"/>
	/// on first call, invoking <see cref="OnStateChanged"/> when notified.
	/// </para>
	/// <para>
	/// Most developers will not need to override this method. If you do, always call
	/// <c>base.SetParametersAsync(parameters)</c> to preserve subscription behavior.
	/// </para>
	/// <para>
	/// This method intentionally avoids async/await. Using await here allows the component
	/// to be disposed between the await point and subsequent code (e.g. during a NavigateTo
	/// in OnInitializedAsync), causing ObjectDisposedException on resume.
	/// </para>
	/// </remarks>
	public override Task SetParametersAsync(ParameterView parameters) {
		var task = base.SetParametersAsync(parameters);
		if (!this._stateSubscribed) {
			this._stateSubscribed = true;
			this.HandleStateChangesFor<TState>(() => {
				if (!this.IsDisposing) {
					this.OnStateChanged();
				}
			});
		}
		return task;
	}

}