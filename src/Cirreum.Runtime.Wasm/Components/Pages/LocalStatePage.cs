namespace Cirreum.Components.Pages;

using Microsoft.AspNetCore.Components;

/// <summary>
/// A base page component that provides dynamic state management with local storage persistence.
/// Extends <see cref="ContainerStatePage{TStateService}"/> with local state integration.
/// </summary>
/// <remarks>
/// <para>This base class provides the same functionality as <see cref="SessionStatePage"/> but with
/// local storage persistence. State persists across browser sessions and survives browser restarts.</para>
/// <para>
/// Use this when you need state to persist longer than a single browser session, such as user preferences
/// or application settings that should be remembered between visits.
/// </para>
/// </remarks>
public abstract class LocalStatePage : ContainerStatePage<ILocalState> {

	/// <summary>
	/// The injected local state service that implements <see cref="ILocalState"/>.
	/// </summary>
	[Inject]
	protected override ILocalState StateService { get; set; } = default!;

	/// <summary>
	/// Handles local state changes in derived components.
	/// </summary>
	/// <remarks>
	/// Called automatically when the <see cref="ILocalState"/> changes.
	/// </remarks>
	protected virtual void OnLocalStateChanged() { }

	private bool localStateInitialized = false;

	/// <inheritdoc/>
	public override Task SetParametersAsync(ParameterView parameters) {
		var task = base.SetParametersAsync(parameters);

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