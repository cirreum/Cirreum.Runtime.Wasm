namespace Cirreum.Components;

/// <summary>
/// A base component that extends <see cref="StateComponentBase"/> with convenience
/// methods for async state types implementing <see cref="IAsyncApplicationState"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AsyncStateComponentBase"/> mirrors <see cref="StateComponentBase"/> but
/// surfaces async-focused method names for clarity. Use this base class when your
/// component subscribes to state that notifies via <c>NotifySubscribersAsync</c> —
/// for example, app user hydration, navigation after authentication, or
/// persistence-driven state.
/// </para>
/// <para>
/// For sync state types implementing <see cref="IApplicationState"/>, use
/// <see cref="StateComponentBase"/> instead.
/// </para>
/// </remarks>
public abstract class AsyncStateComponentBase : StateComponentBase {

	// -------------------------------------------------------------------------
	// Async Subscriptions — IAsyncApplicationState
	// -------------------------------------------------------------------------

	/// <summary>
	/// Registers an async handler invoked with the updated state instance when
	/// <typeparamref name="TState"/> changes via <c>NotifySubscribersAsync</c>.
	/// </summary>
	/// <typeparam name="TState">
	/// The state type to monitor. Must implement <see cref="IAsyncApplicationState"/>.
	/// </typeparam>
	/// <param name="handler">The async callback to invoke when state changes.</param>
	/// <exception cref="InvalidOperationException">
	/// A handler is already registered for <typeparamref name="TState"/>.
	/// </exception>
	protected void HandleAsyncStateChangesFor<TState>(Func<TState, Task> handler)
		where TState : IAsyncApplicationState {
		this.HandleStateChangesForAsync(handler);
	}

	/// <summary>
	/// Registers an async handler invoked when <typeparamref name="TState"/> changes
	/// via <c>NotifySubscribersAsync</c>.
	/// </summary>
	/// <typeparam name="TState">
	/// The state type to monitor. Must implement <see cref="IAsyncApplicationState"/>.
	/// </typeparam>
	/// <param name="handler">The async callback to invoke when state changes.</param>
	/// <exception cref="InvalidOperationException">
	/// A handler is already registered for <typeparamref name="TState"/>.
	/// </exception>
	protected void HandleAsyncStateChangesFor<TState>(Func<Task> handler)
		where TState : IAsyncApplicationState {
		this.HandleStateChangesForAsync<TState>(handler);
	}

	/// <summary>
	/// Subscribes to <typeparamref name="TState"/> async changes and calls
	/// <c>Update</c> when notified, coalesced over
	/// <see cref="StateComponentBase.StateChangeCoalescingDelay"/>.
	/// </summary>
	/// <typeparam name="TState">
	/// The state type to monitor. Must implement <see cref="IAsyncApplicationState"/>.
	/// </typeparam>
	/// <exception cref="InvalidOperationException">
	/// Already subscribed to <typeparamref name="TState"/>.
	/// </exception>
	protected void SubscribeToAsyncStateChanges<TState>()
		where TState : IAsyncApplicationState {
		this.SubscribeToStateChangesAsync<TState>();
	}

	/// <summary>
	/// Notifies all async subscribers of a state change.
	/// </summary>
	/// <typeparam name="TState">
	/// The state type that changed. Must implement <see cref="IAsyncApplicationState"/>.
	/// </typeparam>
	/// <param name="state">The updated state instance.</param>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	protected Task NotifyAsyncSubscribers<TState>(TState state, CancellationToken cancellationToken = default)
		where TState : class, IAsyncApplicationState {
		return this.NotifySubscribersAsync(state, cancellationToken);
	}

	/// <summary>
	/// Cancels an async handler subscription registered via
	/// <see cref="HandleAsyncStateChangesFor{TState}(Func{TState,Task})"/> or
	/// <see cref="HandleAsyncStateChangesFor{TState}(Func{Task})"/>.
	/// </summary>
	/// <typeparam name="TState">The state type whose handler to cancel.</typeparam>
	/// <exception cref="InvalidOperationException">
	/// No async handler is registered for <typeparamref name="TState"/>.
	/// </exception>
	protected void CancelAsyncStateHandler<TState>()
		where TState : IAsyncApplicationState {
		this.CancelStateHandlerAsync<TState>();
	}

	/// <summary>
	/// Cancels an async subscription registered via
	/// <see cref="SubscribeToAsyncStateChanges{TState}"/>.
	/// </summary>
	/// <typeparam name="TState">The state type whose subscription to cancel.</typeparam>
	/// <exception cref="InvalidOperationException">
	/// Not subscribed to async changes for <typeparamref name="TState"/>.
	/// </exception>
	protected void CancelAsyncSubscription<TState>()
		where TState : IAsyncApplicationState {
		this.CancelSubscriptionAsync<TState>();
	}

}