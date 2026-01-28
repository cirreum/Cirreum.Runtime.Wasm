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
///     <item><description>Proper disposal of state subscriptions</description></item>
/// </list>
/// <para>
/// Use this when you need to react to external state changes from other components or services.
/// For dynamic state management, consider <see cref="SessionStatePage"/>, <see cref="LocalStatePage"/>, 
/// or <see cref="MemoryStatePage"/> instead.
/// </para>
/// </remarks>
public abstract class StatePageBase : PageBase {

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
	/// <param name="handler">The callback to invoke when state changes</param>
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
				await this.InvokeAsync(this.StateHasChanged);
			}

		} catch (OperationCanceledException) {
			// Component was disposed, ignore
		} finally {
			this._hasStateChangePending = false;
		}
	}

	/// <summary>
	/// Notifies all subscribers of changes to a state section of type <typeparamref name="TState"/>
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

		if (this.Logger.IsEnabled(LogLevel.Information)) {
			this.Logger.LogInformation("Attempting to cancel subscription handler for type {StateType}", stateType.Name);
		}

		// Check if we have a handler for this state type
		if (!this._handlerSubscriptions.TryGetValue(stateType, out var subscription)) {
			if (this.Logger.IsEnabled(LogLevel.Warning)) {
				this.Logger.LogWarning("Cannot cancel subscription: No handler subscribed to state changes for type {StateType}", stateType.Name);
			}
			throw new InvalidOperationException($"No handler is subscribed for state changes of type {stateType}.");
		}

		// Cancel by calling dispose and removing from private state
		subscription.Dispose();
		this._handlerSubscriptions.Remove(stateType);

		if (this.Logger.IsEnabled(LogLevel.Information)) {
			this.Logger.LogInformation("Successfully cancelled handler subscription for type {StateType}", stateType.Name);
		}

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

		if (this.Logger.IsEnabled(LogLevel.Information)) {
			this.Logger.LogInformation("Attempting to cancel subscription for type {StateType}", stateType.Name);
		}

		// Check if we're subscribed to this state type
		if (!this._internalSubscriptions.TryGetValue(stateType, out var subscription)) {
			if (this.Logger.IsEnabled(LogLevel.Warning)) {
				this.Logger.LogWarning("Cannot cancel subscription: Not subscribed to state changes for type {StateType}", stateType.Name);
			}
			throw new InvalidOperationException($"Not subscribed to state changes for type {stateType}.");
		}

		// Cancel by calling dispose and removing from private state
		subscription.Dispose();
		this._internalSubscriptions.Remove(stateType);

		if (this.Logger.IsEnabled(LogLevel.Information)) {
			this.Logger.LogInformation("Successfully cancelled subscription for type {StateType}", stateType.Name);
		}

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

	bool userInitialized = false;

	/// <summary>
	/// Sets component parameters and subscribes to state changes for <see cref="IUserState"/>.
	/// </summary>
	/// <param name="parameters">The parameters to set.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// IMPORTANT: This method must NOT use 'async/await' pattern to prevent ObjectDisposedException.
	/// When using 'await', the method execution can be interrupted between the await point and the 
	/// subsequent code, allowing the component to be disposed in that interval. This would cause
	/// the subscription attempt to throw an ObjectDisposedException when the method resumes.
	/// </remarks>
	public override Task SetParametersAsync(ParameterView parameters) {

		/*
		 * 
		 * IMPORTANT: This method must NOT use 'async/await' pattern to prevent ObjectDisposedException.
		 * When using 'await', the method execution can be interrupted between the await point and the 
		 * subsequent code, allowing the component to be disposed in that interval. This would cause
		 * the subscription attempt to throw an ObjectDisposedException when the method resumes.
		 *
		 * Example: Page redirect (Navigation.NavigateTo) during an OnInitializedAsync call
		 *
		*/

		// First let the base class set parameters (which will handle DI injections)
		var task = base.SetParametersAsync(parameters);

		// Then use the injected services
		if (!userInitialized) {
			userInitialized = true;
			this.SubscribeToStateChanges<IUserState>();
		}

		return task;

	}

}