namespace Cirreum.Runtime.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides a base class for application pages that support state management, user state subscriptions, and coalesced
/// UI updates. Designed for use with a specific application user type implementing the IApplicationUser interface.
/// </summary>
/// <remarks>ApplicationPageBase enables derived pages to subscribe to state changes, handle user state updates,
/// and manage both synchronous and asynchronous state notifications. It ensures UI updates are coalesced to minimize
/// unnecessary rendering, and provides lifecycle hooks for responding to external and user state changes. Use this base
/// class to build pages that require integration with application state and user context.</remarks>
/// <typeparam name="TApplicationUser">The application user type associated with the page. Must be a class implementing IApplicationUser.</typeparam>
public abstract partial class ApplicationPageBase<TApplicationUser> : PageBase where TApplicationUser : class, IApplicationUser {

	[Inject]
	private IStateManager StateManager { get; set; } = default!;

	private readonly Dictionary<Type, IDisposable> _internalSubscriptions = [];
	private readonly Dictionary<Type, IDisposable> _handlerSubscriptions = [];
	private readonly Dictionary<Type, IDisposable> _internalAsyncSubscriptions = [];
	private readonly Dictionary<Type, IDisposable> _handlerAsyncSubscriptions = [];

	private readonly CancellationTokenSource _cts = new();
	private bool _userStateSubscribed;

	/// <summary>
	/// Gets the delay used to coalesce multiple rapid state changes into a single
	/// <c>StateHasChanged</c> call. Defaults to 16ms (one frame at 60fps).
	/// </summary>
	protected virtual TimeSpan StateChangeCoalescingDelay => TimeSpan.FromMilliseconds(16);

	private volatile bool _hasStateChangePending;

	protected TApplicationUser? AppUser => this.CurrentUser.GetApplicationUser<TApplicationUser>();

	// -------------------------------------------------------------------------
	// Sync Subscriptions — IApplicationState
	// -------------------------------------------------------------------------

	/// <summary>
	/// Registers a handler invoked with the updated state instance when
	/// <typeparamref name="TState"/> changes.
	/// </summary>
	/// <typeparam name="TState">
	/// The state type to monitor. Must implement <see cref="IApplicationState"/>.
	/// </typeparam>
	/// <param name="handler">The callback to invoke when state changes.</param>
	/// <exception cref="InvalidOperationException">
	/// A handler is already registered for <typeparamref name="TState"/>.
	/// </exception>
	protected void HandleStateChangesFor<TState>(Action<TState> handler)
		where TState : IApplicationState {
		ArgumentNullException.ThrowIfNull(handler);
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);
		if (this._handlerSubscriptions.ContainsKey(stateType)) {
			throw new InvalidOperationException($"A subscription handler is already registered for type {stateType.Name}.");
		}
		this._handlerSubscriptions[stateType] = this.StateManager.Subscribe(handler);
	}

	/// <summary>
	/// Registers a handler invoked when <typeparamref name="TState"/> changes.
	/// </summary>
	/// <typeparam name="TState">
	/// The state type to monitor. Must implement <see cref="IApplicationState"/>.
	/// </typeparam>
	/// <param name="handler">The callback to invoke when state changes.</param>
	/// <exception cref="InvalidOperationException">
	/// A handler is already registered for <typeparamref name="TState"/>.
	/// </exception>
	protected void HandleStateChangesFor<TState>(Action handler)
		where TState : IApplicationState {
		ArgumentNullException.ThrowIfNull(handler);
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);
		if (this._handlerSubscriptions.ContainsKey(stateType)) {
			throw new InvalidOperationException($"A subscription handler is already registered for type {stateType.Name}.");
		}
		this._handlerSubscriptions[stateType] = this.StateManager.Subscribe<TState>(handler);
	}

	/// <summary>
	/// Subscribes to <typeparamref name="TState"/> changes and calls
	/// <c>StateHasChanged</c> when notified, coalesced over
	/// <see cref="StateChangeCoalescingDelay"/>.
	/// </summary>
	/// <typeparam name="TState">
	/// The state type to monitor. Must implement <see cref="IApplicationState"/>.
	/// </typeparam>
	/// <exception cref="InvalidOperationException">
	/// Already subscribed to <typeparamref name="TState"/>.
	/// </exception>
	protected void SubscribeToStateChanges<TState>()
		where TState : IApplicationState {
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);
		if (this._internalSubscriptions.ContainsKey(stateType)) {
			throw new InvalidOperationException($"Already subscribed to state changes for type {stateType.Name}.");
		}
		this._internalSubscriptions[stateType] = this.StateManager.Subscribe<TState>(this.QueueStateChange);
	}

	/// <summary>
	/// Notifies all sync subscribers of a state change.
	/// </summary>
	/// <typeparam name="TState">
	/// The state type that changed. Must implement <see cref="IApplicationState"/>.
	/// </typeparam>
	/// <param name="state">The updated state instance.</param>
	protected void NotifySubscribers<TState>(TState state)
		where TState : class, IApplicationState {
		ArgumentNullException.ThrowIfNull(state);
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		this.StateManager.NotifySubscribers(state);
	}

	/// <summary>
	/// Cancels a handler subscription registered via
	/// <see cref="HandleStateChangesFor{TState}(Action{TState})"/> or
	/// <see cref="HandleStateChangesFor{TState}(Action)"/>.
	/// </summary>
	/// <typeparam name="TState">The state type whose handler to cancel.</typeparam>
	/// <exception cref="InvalidOperationException">
	/// No handler is registered for <typeparamref name="TState"/>.
	/// </exception>
	protected void CancelStateHandler<TState>()
		where TState : IApplicationState {
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);
		if (!this._handlerSubscriptions.TryGetValue(stateType, out var subscription)) {
			throw new InvalidOperationException($"No handler is registered for state changes of type {stateType.Name}.");
		}
		subscription.Dispose();
		this._handlerSubscriptions.Remove(stateType);
		Log.CancelledHandler(this.Logger, stateType.Name);
	}

	/// <summary>
	/// Cancels a subscription registered via <see cref="SubscribeToStateChanges{TState}"/>.
	/// </summary>
	/// <typeparam name="TState">The state type whose subscription to cancel.</typeparam>
	/// <exception cref="InvalidOperationException">
	/// Not subscribed to <typeparamref name="TState"/>.
	/// </exception>
	protected void CancelSubscription<TState>()
		where TState : IApplicationState {
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);
		if (!this._internalSubscriptions.TryGetValue(stateType, out var subscription)) {
			throw new InvalidOperationException($"Not subscribed to state changes for type {stateType.Name}.");
		}
		subscription.Dispose();
		this._internalSubscriptions.Remove(stateType);
		Log.CancelledSubscription(this.Logger, stateType.Name);
	}

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
	protected void HandleStateChangesForAsync<TState>(Func<TState, Task> handler)
		where TState : IAsyncApplicationState {
		ArgumentNullException.ThrowIfNull(handler);
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);
		if (this._handlerAsyncSubscriptions.ContainsKey(stateType)) {
			throw new InvalidOperationException($"An async subscription handler is already registered for type {stateType.Name}.");
		}
		this._handlerAsyncSubscriptions[stateType] = this.StateManager.SubscribeAsync(handler);
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
	protected void HandleStateChangesForAsync<TState>(Func<Task> handler)
		where TState : IAsyncApplicationState {
		ArgumentNullException.ThrowIfNull(handler);
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);
		if (this._handlerAsyncSubscriptions.ContainsKey(stateType)) {
			throw new InvalidOperationException($"An async subscription handler is already registered for type {stateType.Name}.");
		}
		this._handlerAsyncSubscriptions[stateType] = this.StateManager.SubscribeAsync<TState>(handler);
	}

	/// <summary>
	/// Subscribes to <typeparamref name="TState"/> async changes and calls
	/// <c>StateHasChanged</c> when notified, coalesced over
	/// <see cref="StateChangeCoalescingDelay"/>.
	/// </summary>
	/// <typeparam name="TState">
	/// The state type to monitor. Must implement <see cref="IAsyncApplicationState"/>.
	/// </typeparam>
	/// <exception cref="InvalidOperationException">
	/// Already subscribed to <typeparamref name="TState"/>.
	/// </exception>
	protected void SubscribeToStateChangesAsync<TState>()
		where TState : IAsyncApplicationState {
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);
		if (this._internalAsyncSubscriptions.ContainsKey(stateType)) {
			throw new InvalidOperationException($"Already subscribed to async state changes for type {stateType.Name}.");
		}
		this._internalAsyncSubscriptions[stateType] = this.StateManager.SubscribeAsync<TState>(this.QueueStateChangeAsync);
	}

	/// <summary>
	/// Notifies all async subscribers of a state change.
	/// </summary>
	/// <typeparam name="TState">
	/// The state type that changed. Must implement <see cref="IAsyncApplicationState"/>.
	/// </typeparam>
	/// <param name="state">The updated state instance.</param>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	protected Task NotifySubscribersAsync<TState>(TState state, CancellationToken cancellationToken = default)
		where TState : class, IAsyncApplicationState {
		ArgumentNullException.ThrowIfNull(state);
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		return this.StateManager.NotifySubscribersAsync(state, cancellationToken);
	}

	/// <summary>
	/// Cancels an async handler subscription registered via
	/// <see cref="HandleStateChangesForAsync{TState}(Func{TState,Task})"/> or
	/// <see cref="HandleStateChangesForAsync{TState}(Func{Task})"/>.
	/// </summary>
	/// <typeparam name="TState">The state type whose handler to cancel.</typeparam>
	/// <exception cref="InvalidOperationException">
	/// No async handler is registered for <typeparamref name="TState"/>.
	/// </exception>
	protected void CancelStateHandlerAsync<TState>()
		where TState : IAsyncApplicationState {
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);
		if (!this._handlerAsyncSubscriptions.TryGetValue(stateType, out var subscription)) {
			throw new InvalidOperationException($"No async handler is registered for state changes of type {stateType.Name}.");
		}
		subscription.Dispose();
		this._handlerAsyncSubscriptions.Remove(stateType);
		Log.CancelledHandler(this.Logger, stateType.Name);
	}

	/// <summary>
	/// Cancels an async subscription registered via
	/// <see cref="SubscribeToStateChangesAsync{TState}"/>.
	/// </summary>
	/// <typeparam name="TState">The state type whose subscription to cancel.</typeparam>
	/// <exception cref="InvalidOperationException">
	/// Not subscribed to async changes for <typeparamref name="TState"/>.
	/// </exception>
	protected void CancelSubscriptionAsync<TState>()
		where TState : IAsyncApplicationState {
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);
		if (!this._internalAsyncSubscriptions.TryGetValue(stateType, out var subscription)) {
			throw new InvalidOperationException($"Not subscribed to async state changes for type {stateType.Name}.");
		}
		subscription.Dispose();
		this._internalAsyncSubscriptions.Remove(stateType);
		Log.CancelledSubscription(this.Logger, stateType.Name);
	}

	// -------------------------------------------------------------------------
	// Coalescing
	// -------------------------------------------------------------------------

	private void QueueStateChange() {
		if (this._cts.Token.IsCancellationRequested) {
			return;
		}
		if (!this._hasStateChangePending) {
			this._hasStateChangePending = true;
			_ = this.ScheduleStateHasChanged();
		}
	}

	private Task QueueStateChangeAsync() {
		this.QueueStateChange();
		return Task.CompletedTask;
	}

	private async Task ScheduleStateHasChanged() {
		try {
			if (this._cts.Token.IsCancellationRequested) {
				return;
			}
			await Task.Delay(this.StateChangeCoalescingDelay, this._cts.Token);
			if (!this._cts.Token.IsCancellationRequested) {
				await this.InvokeAsync(this.OnStateChanged);
			}
		} catch (OperationCanceledException) {
			// Component was disposed — ignore
		} finally {
			this._hasStateChangePending = false;
		}
	}

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
	protected virtual void OnStateChanged() {
		this.StateHasChanged();
	}

	/// <summary>
	/// Invokes logic to notify that the user state has changed, triggering any necessary updates.
	/// </summary>
	protected virtual void OnUserStateChanged() {
		this.StateHasChanged();
	}

	// -------------------------------------------------------------------------
	// Lifecycle
	// -------------------------------------------------------------------------

	/// <summary>
	/// Sets component parameters and subscribes to <see cref="IUserState"/> changes.
	/// </summary>
	/// <remarks>
	/// IMPORTANT: This method intentionally avoids async/await. Using await here allows
	/// the component to be disposed between the await point and subsequent code (e.g. during
	/// a NavigateTo call in OnInitializedAsync), causing ObjectDisposedException on resume.
	/// </remarks>
	public override Task SetParametersAsync(ParameterView parameters) {
		var task = base.SetParametersAsync(parameters);
		if (!this._userStateSubscribed) {
			this._userStateSubscribed = true;
			this.HandleStateChangesFor<IUserState>(() => _ = this.InvokeAsync(this.OnUserStateChanged));
		}
		return task;
	}

	protected override void Dispose(bool disposing) {
		if (disposing) {
			foreach (var subscription in this._internalSubscriptions.Values) {
				subscription.Dispose();
			}
			foreach (var subscription in this._handlerSubscriptions.Values) {
				subscription.Dispose();
			}
			foreach (var subscription in this._internalAsyncSubscriptions.Values) {
				subscription.Dispose();
			}
			foreach (var subscription in this._handlerAsyncSubscriptions.Values) {
				subscription.Dispose();
			}
			this._cts.Cancel();
			this._cts.Dispose();
		}
		base.Dispose(disposing);
	}

	// -------------------------------------------------------------------------
	// Logging
	// -------------------------------------------------------------------------

	private static partial class Log {
		[LoggerMessage(Level = LogLevel.Information, Message = "Cancelled state handler subscription for {StateType}")]
		internal static partial void CancelledHandler(ILogger logger, string stateType);

		[LoggerMessage(Level = LogLevel.Information, Message = "Cancelled state subscription for {StateType}")]
		internal static partial void CancelledSubscription(ILogger logger, string stateType);
	}

}