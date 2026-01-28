namespace Cirreum.Components;

using Cirreum.Components.Pages;
using Microsoft.AspNetCore.Components;

/// <summary>
/// A base component that provides state management capabilities backed by <see cref="IMemoryState"/>
/// which is retained in-memory only.
/// </summary>
/// <remarks>
/// <list type="bullet">
///     <item>
///         <description>Automatically subscribes to and handles memory state changes</description>
///     </item>
///     <item>
///         <description>Provides the <see cref="OnMemoryStateChanged"/> method for derived components to handle state updates</description>
///     </item>
///     <item>
///         <description>Manages proper lifecycle management to prevent memory leaks</description>
///     </item>
/// </list>
/// </remarks>
public abstract class MemoryStateComponent : ContainerStatePage<IMemoryState> {

	/// <summary>
	/// The injected memory state service that implements <see cref="IMemoryState"/>.
	/// </summary>
	/// <value>
	/// An instance of <see cref="IMemoryState"/> provided by Blazor's dependency injection system.
	/// </value>
	[Inject]
	protected override IMemoryState StateService { get; set; } = default!;

	/// <summary>
	/// Override this method to handle memory state changes.
	/// </summary>
	/// <remarks>
	/// Called automatically when the <see cref="IMemoryState"/> changes.
	/// </remarks>
	protected virtual void OnMemoryStateChanged() { }

	private bool memoryStateInitialized = false;

	/// <summary>
	/// Sets component parameters and initializes memory state handling.
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
		if (!memoryStateInitialized) {
			memoryStateInitialized = true;
			this.HandleStateChangesFor<IMemoryState>(s => {
				if (!this.IsDisposing) {
					this.OnMemoryStateChanged();
				}
			});
		}

		return task;

	}

}