namespace Cirreum.Components.Pages;

using Microsoft.AspNetCore.Components;

/// <summary>
/// A base page component that provides dynamic state management with session storage persistence.
/// Extends <see cref="ContainerStatePage{TStateService}"/> with session state integration.
/// </summary>
/// <remarks>
/// <para>This base class provides:</para>
/// <list type="bullet">
///     <item>
///         <description>Dynamic property creation without explicit configuration</description>
///     </item>
///     <item>
///         <description>Automatic persistence to browser session storage</description>
///     </item>
///     <item>
///         <description><c>Get</c>, <c>Set</c>, and <c>GetOrCreate</c> methods for state management</description>
///     </item>
///     <item>
///         <description>All services from <see cref="StatePageBase"/> (Logger, CurrentUser, JSApp, Mediatr, Dialogr, Toastr)</description>
///     </item>
/// </list>
/// <para>
/// This class is ideal for pages that need simple, dynamic state management without form validation overhead.
/// State persists across page refreshes and browser navigation within the same session.
/// </para>
/// <para>
/// For complex forms with validation requirements, consider using <c>SessionStateViewModel</c> with <c>EditContext</c> integration instead.
/// For custom state interfaces, use <c>StatePageBase&lt;T&gt;</c> or its specialized variants.
/// </para>
/// </remarks>
public abstract class SessionStatePage : ContainerStatePage<ISessionState> {

	/// <summary>
	/// The injected session state service that implements <see cref="ISessionState"/>.
	/// </summary>
	[Inject]
	protected override ISessionState StateService { get; set; } = default!;

	/// <summary>
	/// Handle session state changes in derived components.
	/// </summary>
	/// <remarks>
	/// Called automatically when the <see cref="ISessionState"/> changes.
	/// </remarks>
	protected virtual void OnSessionStateChanged() { }

	private bool sessionInitialized = false;

	/// <inheritdoc/>
	public override Task SetParametersAsync(ParameterView parameters) {
		var task = base.SetParametersAsync(parameters);

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