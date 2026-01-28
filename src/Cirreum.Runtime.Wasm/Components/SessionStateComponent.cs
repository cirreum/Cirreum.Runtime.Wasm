namespace Cirreum.Components;

using Cirreum.Components.Pages;
using Microsoft.AspNetCore.Components;

/// <summary>
/// A base component that provides state management capabilities backed by <see cref="ISessionState"/>
/// which is persisted to the browsers session storage.
/// </summary>
/// <remarks>
/// <list type="bullet">
///     <item>
///         <description>Automatically subscribes to and handles session state changes</description>
///     </item>
///     <item>
///         <description>Provides the <see cref="OnSessionStateChanged"/> method for derived components to handle state updates</description>
///     </item>
///     <item>
///         <description>Manages proper lifecycle management to prevent memory leaks</description>
///     </item>
/// </list>
/// </remarks>
public abstract class SessionStateComponent : ContainerStatePage<ISessionState> {

	/// <summary>
	/// The injected session state service that implements <see cref="ISessionState"/>.
	/// </summary>
	/// <value>
	/// An instance of <see cref="ISessionState"/> provided by Blazor's dependency injection system.
	/// </value>
	[Inject]
	protected override ISessionState StateService { get; set; } = default!;

	/// <summary>
	/// Override this method to handle session state changes.
	/// </summary>
	/// <remarks>
	/// Called automatically when the <see cref="ISessionState"/> changes.
	/// </remarks>
	protected virtual void OnSessionStateChanged() {

	}

	private bool sessionInitialized = false;

	/// <summary>
	/// Sets component parameters and initializes session state handling.
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

		// First let the base class set parameters (which will handle DI injections)
		var task = base.SetParametersAsync(parameters);

		// Then use the injected services
		if (!sessionInitialized) {
			sessionInitialized = true;
			this.HandleStateChangesFor<ISessionState>(s => {
				if (!this.IsDisposing) {
					this.OnSessionStateChanged();
				}
			});
		}

		return task;

	}

}