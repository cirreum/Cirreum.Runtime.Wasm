namespace Cirreum.Components;

using Microsoft.AspNetCore.Components;

/// <summary>
/// A base component that provides state management capabilities backed by <see cref="ILocalState"/>
/// which is persisted to the browsers local storage.
/// </summary>
/// <remarks>
/// <list type="bullet">
///     <item>
///         <description>Automatically subscribes to and handles local state changes</description>
///     </item>
///     <item>
///         <description>Provides the <see cref="OnLocalStateChanged"/> method for derived components to handle state updates</description>
///     </item>
///     <item>
///         <description>Manages proper lifecycle management to prevent memory leaks</description>
///     </item>
/// </list>
/// </remarks>
public abstract class LocalStateComponent : ContainerStatePage<ILocalState> {

	/// <summary>
	/// The injected local state service that implements <see cref="ILocalState"/>.
	/// </summary>
	/// <value>
	/// An instance of <see cref="ILocalState"/> provided by Blazor's dependency injection system.
	/// </value>
	[Inject]
	protected override ILocalState StateService { get; set; } = default!;

	/// <summary>
	/// Override this method to handle local state changes.
	/// </summary>
	/// <remarks>
	/// Called automatically when the <see cref="ILocalState"/> changes.
	/// </remarks>
	protected virtual void OnLocalStateChanged() {

	}

	private bool localStateInitialized = false;

	/// <summary>
	/// Sets component parameters and initializes local state handling.
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
		if (!localStateInitialized) {
			localStateInitialized = true;
			this.HandleStateChangesFor<ILocalState>(s => {
				if (!this.IsDisposing) {
					this.OnLocalStateChanged();
				}
			});
		}

		return task;

	}

}