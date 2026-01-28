namespace Cirreum.Components;

using Microsoft.AspNetCore.Components;

/// <summary>
/// A base component that provides strongly-typed state management integration. Extends <see cref="StateComponentBase"/>
/// with automatic injection of a specific <typeparamref name="TState"/> state type.
/// </summary>
/// <typeparam name="TState">The type of state to inject. Must implement <see cref="IApplicationState"/>.</typeparam>
/// <remarks>
/// <para>This generic base class provides:</para>
/// <list type="bullet">
///     <item>
///         <description>Automatic injection of <typeparamref name="TState"/> as the <see cref="State"/> property</description>
///     </item>
///     <item>
///         <description>Virtual <see cref="OnStateChanged"/> method for handling external state changes</description>
///     </item>
///     <item>
///         <description>All services from <see cref="StateComponentBase"/> (Logger, CurrentUser, JSApp, Mediatr, Dialogr, Toastr)</description>
///     </item>
/// </list>
/// <para>
/// Use this base class when you want strongly-typed access to a specific state interface.
/// For basic state management with built-in state types, consider using <see cref="SessionStateComponent"/>, 
/// <see cref="LocalStateComponent"/>, or <see cref="MemoryStateComponent"/> instead.
/// </para>
/// </remarks>
public abstract class StateComponentBase<TState> : StateComponentBase
	where TState : IApplicationState {

	/// <summary>
	/// The injected state service of type <typeparamref name="TState"/>.
	/// </summary>
	/// <value>
	/// An instance of <typeparamref name="TState"/> provided by Blazor's dependency injection system.
	/// </value>
	[Inject]
	protected TState State { get; set; } = default!;

	/// <summary>
	/// Called when the injected state has changed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Override this method in derived classes to handle state changes that occur outside 
	/// of the current component (e.g., from other components, background services, or external events).
	/// </para>
	/// <para>
	/// Note: This method is only called for external state changes. Component UI State changes triggered by 
	/// user interactions within this component will automatically trigger re-rendering without 
	/// calling this method.
	/// </para>
	/// </remarks>
	protected virtual void OnStateChanged() { }

	private bool stateInitialized = false;

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// Automatically subscribes to state changes for <typeparamref name="TState"/> during component initialization.
	/// This ensures that <see cref="OnStateChanged"/> is called when external modifications occur to the state.
	/// </para>
	/// <para>
	/// Most developers will not need to override this method. If you do override it, ensure you call 
	/// <c>base.SetParametersAsync(parameters)</c> to maintain proper state subscription behavior.
	/// </para>
	/// </remarks>
	public override Task SetParametersAsync(ParameterView parameters) {
		// we do this here as most users won't need to override it
		// versus OnInitializedAsync were they'd have to remember
		// to call base.OnInitializedAsync()

		// First let the base class set parameters (which will handle DI injections)
		var task = base.SetParametersAsync(parameters);

		// Then use the injected services
		if (!stateInitialized) {
			stateInitialized = true;
			this.HandleStateChangesFor<TState>(() => {
				if (!this.IsDisposing) {
					this.OnStateChanged();
				}
			});
		}

		return task;
	}

}