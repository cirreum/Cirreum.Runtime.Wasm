namespace Cirreum.Components.ViewModels;

using Microsoft.AspNetCore.Components.Forms;

/// <summary>
/// Defines the contract for a view model that integrates with 
/// <see cref="Microsoft.AspNetCore.Components.Forms.EditContext"/> and <see cref="EditForm"/>
/// via the <see cref="IFieldIdentifierProvider"/> interface.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to create view models that integrate with Blazor's form system
/// and validation components by extending the <see cref="IFieldIdentifierProvider"/> interface.
/// </para>
/// </remarks>
public interface IViewModel : IFieldIdentifierProvider {

	/// <summary>
	/// The current <see cref="Microsoft.AspNetCore.Components.Forms.EditContext"/> for this view model
	/// for use with <see cref="EditForm"/>.
	/// </summary>
	EditContext EditContext { get; }

	/// <summary>
	/// Resets the value for all registered properties by writing their originally provided default value to
	/// state.
	/// </summary>
	/// <returns>A task representing the asynchronous reset operation.</returns>
	/// <remarks>
	/// <para>
	/// If a <c>Set</c> or <c>Reset</c> operation is already occurring, calls to this method
	/// will be a no-op.
	/// </para>
	/// <para>
	/// To prevent unnecessary state change notifications, this method will attempt to use
	/// <c>CreateNotificationScope</c> to only send a single notification once all
	/// properties have been reset, if available on the backing state.
	/// </para>
	/// </remarks>
	Task Reset();

}