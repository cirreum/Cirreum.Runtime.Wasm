namespace Cirreum.Components;

using Cirreum;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base component that provides State management capabilities for Blazor components.
/// </summary>
/// <remarks>
/// <para>
/// Provides functionality for:
/// <list type="bullet">
///     <item>
///         <description>Subscribing to state changes</description>
///     </item>
///     <item>
///         <description>Handling state updates with custom callbacks</description>
///     </item>
///     <item>
///         <description>Coalescing rapid state changes to prevent excessive renders</description>
///     </item>
///     <item>
///         <description>Automatic cleanup of subscriptions on disposal</description>
///     </item>
/// </list>
/// </para>
/// </remarks>
public abstract class StateComponentBase : BaseAfterRenderComponent {

	[Inject]
	private ILoggerFactory LogFactory { get; set; } = default!;
	private ILogger? _logger;
	/// <summary>
	/// An <see cref="ILogger{TCategoryName}"/>.
	/// </summary>
	/// <remarks>
	/// The category is derived from the <c>Type</c> of this page instance.
	/// </remarks>
	protected ILogger Logger => this._logger ??= this.LogFactory.CreateLogger(this.GetType());

	[Inject]
	private IStateManager StateManager { get; set; } = default!;

	private readonly Dictionary<Type, IDisposable> _internalSubscriptions = [];
	private readonly Dictionary<Type, IDisposable> _handlerSubscriptions = [];
	private readonly CancellationTokenSource _cts = new();
	protected virtual TimeSpan StateChangeCoalescingDelay => TimeSpan.FromMilliseconds(16);
	private volatile bool _hasStateChangePending;

	/// <summary>
	/// Registers a handler to be invoked when changes to state <typeparamref name="TState"/> occur.
	/// </summary>
	/// <typeparam name="TState">Type of the state to handle changes for.</typeparam>
	/// <param name="handler">The callback to invoke when state changes</param>
	/// <exception cref="InvalidOperationException">A handler is already subscribed to state changes for type {stateType}</exception>
	protected void HandleStateChangesFor<TState>(Action<TState> handler) where TState : IApplicationState {
		ArgumentNullException.ThrowIfNull(handler);
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);
		if (this._handlerSubscriptions.ContainsKey(stateType)) {
			throw new InvalidOperationException($"A subscription handler is already subscribed to state changes for type {stateType}.");
		}
		this._handlerSubscriptions[stateType]
			= this.StateManager.Subscribe(handler);
	}

	/// <summary>
	/// Registers a handler to be invoked when changes to state <typeparamref name="TState"/> occur.
	/// </summary>
	/// <typeparam name="TState">Type of the state to handle changes for.</typeparam>
	/// <param name="handler">The callback to invoke when the state has changed</param>
	/// <exception cref="InvalidOperationException">A handler is already subscribed to state changes for type {stateType}</exception>
	protected void HandleStateChangesFor<TState>(Action handler) where TState : IApplicationState {
		ArgumentNullException.ThrowIfNull(handler);
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);
		if (this._handlerSubscriptions.ContainsKey(stateType)) {
			throw new InvalidOperationException($"A subscription handler is already subscribed to state changes for type {stateType}.");
		}
		this._handlerSubscriptions[stateType]
			= this.StateManager.Subscribe<TState>(handler);
	}

	/// <summary>
	/// Subscribe to state changes for the <typeparamref name="TState"/> state
	/// and calls <c>StateHasChanged</c>.
	/// </summary>
	/// <typeparam name="TState">Type of the state to handle changes for.</typeparam>
	/// <remarks>
	/// The subscription is coalesced with other subscriptions with a configurable delay
	/// (see: <see cref="StateChangeCoalescingDelay"/>), so <c>StateHasChanged</c> is only called once
	/// within that period.
	/// </remarks>
	/// <exception cref="InvalidOperationException">Already subscribed to state changes for type {stateType}</exception>
	protected void SubscribeToStateChanges<TState>() where TState : IApplicationState {
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);
		if (this._internalSubscriptions.ContainsKey(stateType)) {
			throw new InvalidOperationException($"Already subscribed to state changes for type {stateType}.");
		}
		this._internalSubscriptions[stateType]
			= this.StateManager.Subscribe<TState>(this.QueueStateChange);
	}
	private void QueueStateChange() {
		if (this._cts.Token.IsCancellationRequested) {
			return;
		}
		if (!this._hasStateChangePending) {
			this._hasStateChangePending = true;
			_ = this.ScheduleStateHasChanged(); // Fire and forget, but capture exceptions
		}
	}
	private async Task ScheduleStateHasChanged() {
		try {

			// no need to wait, if we're disposed
			if (this._cts.Token.IsCancellationRequested) {
				return;
			}

			// Wait for a short period to coalesce multiple updates
			await Task.Delay(this.StateChangeCoalescingDelay, this._cts.Token);

			// Only trigger Update if we haven't been disposed
			if (!this._cts.Token.IsCancellationRequested) {
				await this.InvokeAsync(this.Update);
			}

		} catch (OperationCanceledException) {
			// Component was disposed, ignore
		} finally {
			this._hasStateChangePending = false;
		}
	}

	/// <summary>
	/// Notifies all subscribers of a change in state for type <typeparamref name="TState"/>
	/// </summary>
	/// <typeparam name="TState">Type of the state section that changed</typeparam>
	/// <param name="stateSection">The updated state section</param>
	/// <exception cref="ObjectDisposedException">The page has been disposed.</exception>
	protected void NotifySubscribers<TState>(TState stateSection) where TState : class, IApplicationState {
		ArgumentNullException.ThrowIfNull(stateSection);
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		this.StateManager.NotifySubscribers(stateSection);
	}

	/// <summary>
	/// Cancels the handler for state changes of type <typeparamref name="TState"/>.
	/// </summary>
	/// <typeparam name="TState">Type of the state to cancel handler for.</typeparam>
	/// <exception cref="InvalidOperationException">No handler is subscribed for state changes of type {stateType}.</exception>
	/// <exception cref="ObjectDisposedException">The component has been disposed.</exception>
	protected void CancelStateHandler<TState>() where TState : IApplicationState {
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);

		this.Logger.LogInformation("Attempting to cancel subscription handler for type {StateType}", stateType.Name);

		// Check if we have a handler for this state type
		if (!this._handlerSubscriptions.TryGetValue(stateType, out var subscription)) {
			this.Logger.LogWarning("Cannot cancel subscription: No handler subscribed to state changes for type {StateType}", stateType.Name);
			throw new InvalidOperationException($"No handler is subscribed for state changes of type {stateType}.");
		}

		// Cancel by calling dispose and removing from private state
		subscription.Dispose();
		this._handlerSubscriptions.Remove(stateType);

		this.Logger.LogInformation("Successfully cancelled handler subscription for type {StateType}", stateType.Name);

	}

	/// <summary>
	/// Cancels the subscription for state changes of type <typeparamref name="TState"/>.
	/// </summary>
	/// <typeparam name="TState">Type of the state to cancel subscription for.</typeparam>
	/// <exception cref="InvalidOperationException">Not subscribed to state changes for type {stateType}.</exception>
	/// <exception cref="ObjectDisposedException">The component has been disposed.</exception>
	protected void CancelSubscription<TState>() where TState : IApplicationState {
		ObjectDisposedException.ThrowIf(this._cts.Token.IsCancellationRequested, this);
		var stateType = typeof(TState);

		this.Logger.LogInformation("Attempting to cancel subscription for type {StateType}", stateType.Name);

		// Check if we're subscribed to this state type
		if (!this._internalSubscriptions.TryGetValue(stateType, out var subscription)) {
			this.Logger.LogWarning("Cannot cancel subscription: Not subscribed to state changes for type {StateType}", stateType.Name);
			throw new InvalidOperationException($"Not subscribed to state changes for type {stateType}.");
		}

		// Cancel by calling dispose and removing from private state
		subscription.Dispose();
		this._internalSubscriptions.Remove(stateType);

		this.Logger.LogInformation("Successfully cancelled subscription for type {StateType}", stateType.Name);

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

}