namespace Cirreum.Components;

using Microsoft.AspNetCore.Components;

/// <summary>
/// A base component that provides strongly-typed async state management integration.
/// Extends <see cref="AsyncStateComponentBase"/> with automatic injection of a specific
/// <typeparamref name="TState"/> async state type.
/// </summary>
/// <typeparam name="TState">
/// The type of state to inject. Must implement <see cref="IAsyncApplicationState"/>.
/// For sync state types implementing <see cref="IApplicationState"/>, use
/// <see cref="StateComponentBase{TState}"/> instead.
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
///         Virtual <see cref="OnStateChangedAsync"/> called when the injected state changes
///         via <c>NotifySubscribersAsync</c>.
///         </description>
///     </item>
///     <item>
///         <description>
///         All services from <see cref="StateComponentBase"/> (Logger, state subscriptions).
///         </description>
///     </item>
/// </list>
/// <para>
/// Use this base class when your component needs to react to async state — for example,
/// rendering based on authenticated user state or responding to persistence-driven changes.
/// For sync state types, use <see cref="StateComponentBase{TState}"/> instead.
/// </para>
/// <example>
/// <code>
/// // React to authenticated user state changes in a component
/// public class UserAvatarComponent : AsyncStateComponentBase&lt;IClientUserState&gt;
/// {
///     protected override async Task OnStateChangedAsync()
///     {
///         await InvokeAsync(StateHasChanged);
///     }
/// }
/// </code>
/// </example>
/// </remarks>
public abstract class AsyncStateComponentBase<TState> : AsyncStateComponentBase
	where TState : IAsyncApplicationState {

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
	/// Called when the injected state has changed via <c>NotifySubscribersAsync</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Override in derived classes to react to async state changes — app user hydration,
	/// navigation after authentication, or persistence-driven state updates.
	/// </para>
	/// <para>
	/// The updated state is available via the <see cref="State"/> property.
	/// </para>
	/// <para>
	/// Note: This is only called for external state changes. UI interactions within
	/// this component trigger re-rendering directly without calling this method.
	/// </para>
	/// </remarks>
	protected virtual Task OnStateChangedAsync() => Task.CompletedTask;

	// -------------------------------------------------------------------------
	// Lifecycle
	// -------------------------------------------------------------------------

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// Automatically subscribes to async state changes for <typeparamref name="TState"/>
	/// on first call, invoking <see cref="OnStateChangedAsync"/> when notified.
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
			this.HandleStateChangesForAsync<TState>(async _ => {
				if (!this.IsDisposing) {
					await this.OnStateChangedAsync();
				}
			});
		}
		return task;
	}

}