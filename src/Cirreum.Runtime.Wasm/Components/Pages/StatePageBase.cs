namespace Cirreum.Components.Pages;

using Cirreum;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// A base page component that extends <see cref="PageBase"/> with state management capabilities.
/// </summary>
/// <remarks>
/// <para>Provides all services from <see cref="PageBase"/> plus state management infrastructure:</para>
/// <list type="bullet">
///     <item><description>State change subscriptions and notifications</description></item>
///     <item><description>Automatic user state monitoring</description></item>
///     <item><description>State change coalescing to optimize re-rendering</description></item>
///     <item><description>Proper disposal of all state subscriptions</description></item>
/// </list>
/// <para>
/// Use this when you need to react to external state changes from other components or services.
/// For dynamic state management, consider <see cref="SessionStatePage"/>, <see cref="LocalStatePage"/>,
/// or <see cref="MemoryStatePage"/> instead.
/// </para>
/// </remarks>
public abstract partial class StatePageBase : PageBase {

	[Inject]
	private IStateManager StateManager { get; set; } = default!;

	private readonly Dictionary<Type, IDisposable> _internalSubscriptions = [];
	private readonly Dictionary<Type, IDisposable> _handlerSubscriptions = [];
	private readonly CancellationTokenSource _cts = new();
	private bool _userStateSubscribed;

	/// <summary>
	/// Gets the delay used to coalesce multiple rapid state changes into a single
	/// <c>StateHasChanged</c> call. Defaults to 16ms (one frame at 60fps).
	/// </summary>
	protected virtual TimeSpan StateChangeCoalescingDelay => TimeSpan.FromMilliseconds(16);

	private volatile bool _hasStateChangePending;

	// -------------------------------------------------------------------------
	// Subscriptions
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

	private async Task ScheduleStateHasChanged() {
		try {
			if (this._cts.Token.IsCancellationRequested) {
				return;
			}
			await Task.Delay(this.StateChangeCoalescingDelay, this._cts.Token);
			if (!this._cts.Token.IsCancellationRequested) {
				await this.InvokeAsync(this.StateHasChanged);
			}
		} catch (OperationCanceledException) {
			// Component was disposed — ignore
		} finally {
			this._hasStateChangePending = false;
		}
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
			this.SubscribeToStateChanges<IUserState>();
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