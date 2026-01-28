namespace Cirreum.Components.ViewModels;

using Cirreum.Components.Validation;

/// <summary>
/// A convenient base View Model with dynamic bindable properties that are persisted with the 
/// <see cref="ISessionState"/> service, including type-safe access, UI notifications and validation.
/// </summary>
/// <typeparam name="TViewModel">The concrete implementation of the view model.</typeparam>
/// <remarks>
/// <para>
/// Implements <see cref="IViewModel"/> to provide integration with Blazor's
/// <see cref="Microsoft.AspNetCore.Components.Forms.EditContext"/>, <see cref="FluentValidationValidator"/>
/// and <see cref="InvalidMessage{TValue}"/>
/// </para>
/// <para>
/// Requires the implementor to override <c>Configure</c> to create the backing methods for the
/// properties of the view model.
/// </para>
/// <para>
/// Optionally, implement <see cref="IDisposable"/> and call <c>Clear</c>
/// to prevent the properties from living in session storage beyond the life of the page.
/// </para>
/// <para>
/// To prevent notification thrashing, it is recommended to use the 
/// <c>CreateNotificationScope</c> method when mutating state on multiple
/// properties at once.
/// </para>
/// <para>
/// <b>Sample scoped state key:</b> '{scope}.MyPageWithSessionViewModel.SearchTerm'
/// </para>
/// <para>
/// Example usage:
/// <code>
/// public class MyPageWithSessionViewModel(
///	    ISessionState session, 
///	    string scope
///	) : SessionViewModel&lt;MyPageWithSessionViewModel&gt;(session, scope)
///	  , IDisposable {
///     
///     public string SearchTerm => Get&lt;string&gt;();
///     public Task SetSearchTerm(string value) => Set(value);
///     
///     protected override void Configure() => this
///	        .AddProperty(s => s.SearchTerm, string.Empty);
///			
///	    public void Dispose() {
///	        this.Remove();
///	    }
/// }
/// </code>
/// </para>
/// </remarks>
/// <remarks>
/// Constructs a new view model instance.
/// </remarks>
/// <param name="session">The <see cref="ISessionState"/> service.</param>
/// <param name="scope">The name in which the session data will be scoped to.</param>
public abstract class SessionStateViewModel<TViewModel>(
	ISessionState session,
	string scope
) : PersistentStateViewModel<TViewModel, ISessionState>(session, scope)
	where TViewModel : SessionStateViewModel<TViewModel> {
}