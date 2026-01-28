namespace Cirreum.Components.Pages;

using Microsoft.AspNetCore.Components;

/// <summary>
/// A base page component that provides dynamic state management with memory-only storage.
/// Extends <see cref="ContainerStatePage{TStateService}"/> with memory state integration.
/// </summary>
/// <remarks>
/// <para>This base class provides the same API as <see cref="SessionStatePage"/> and <see cref="LocalStatePage"/>
/// but with memory-only persistence. State is lost when the page is disposed or the application restarts.</para>
/// <para>
/// Use this for temporary state that doesn't need to persist, such as UI state, temporary calculations,
/// or when you want an API without any persistence overhead.
/// </para>
/// </remarks>
public abstract class MemoryStatePage : ContainerStatePage<IMemoryState> {

	/// <summary>
	/// The injected memory state service that implements <see cref="IMemoryState"/>.
	/// </summary>
	[Inject]
	protected override IMemoryState StateService { get; set; } = default!;

	/// <summary>
	/// Handle memory state changes in derived components.
	/// </summary>
	/// <remarks>
	/// Called automatically when the <see cref="IMemoryState"/> changes.
	/// </remarks>
	protected virtual void OnMemoryStateChanged() { }

	private bool memoryInitialized = false;

	/// <inheritdoc/>
	public override Task SetParametersAsync(ParameterView parameters) {
		var task = base.SetParametersAsync(parameters);

		if (!memoryInitialized) {
			memoryInitialized = true;
			this.HandleStateChangesFor<IMemoryState>(s => {
				if (!this.IsDisposing) {
					this.OnMemoryStateChanged();
				}
			});
		}

		return task;
	}

}