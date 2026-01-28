namespace Cirreum.Components.ViewModels;

using Cirreum.Components.Validation;
using Microsoft.AspNetCore.Components.Forms;
using System.Collections.Concurrent;

/// <summary>
/// A class that provides a base implementation of the <see cref="IViewModel"/> for POCO-style view models.
/// </summary>
/// <remarks>
/// <para>
/// Implements <see cref="IViewModel"/> to provide integration with Blazor's
/// <see cref="Microsoft.AspNetCore.Components.Forms.EditContext"/>, <see cref="FluentValidationValidator"/>
/// and <see cref="InvalidMessage{TValue}"/>
/// </para>
/// <para>
/// For view models that need custom field identification or reset behavior, override 
/// <see cref="GetFieldIdentifier"/> or <see cref="Reset"/> respectively.
/// </para>
/// <para>
/// Maintains a thread-safe cache of <see cref="FieldIdentifier"/> instances per property.
/// </para>
/// </remarks>
public abstract class ViewModel : IViewModel {

	// Cache of FieldIdentifier instances to avoid recreating them
	private readonly ConcurrentDictionary<string, FieldIdentifier> _fields = [];

	/// <inheritdoc/>
	public EditContext EditContext { get; private set; }

	/// <summary>
	/// Constructs a new view model instance with its own <see cref="EditContext"/>.
	/// </summary>
	protected ViewModel() {
		this.EditContext = new EditContext(this);
	}

	/// <inheritdoc/>
	public virtual FieldIdentifier? GetFieldIdentifier(string propertyName)
		=> this._fields.GetOrAdd(propertyName, name => this.EditContext.Field(name));

	/// <summary>
	/// Resets the current state of the object to its default values.
	/// </summary>
	/// <remarks>This method resets all properties to their default values and clears any existing validation state.
	/// It creates a new <see cref="EditContext"/> instance to ensure validation state is fully reset.</remarks>
	/// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
	public Task Reset() {

		// Reset properties to defaults
		this.ResetProperties();

		// Create a new EditContext to clear all validation state
		this.EditContext = new EditContext(this);

		return Task.CompletedTask;
	}

	/// <summary>
	/// Return the view model properties back to their initial default values.
	/// </summary>
	protected abstract void ResetProperties();

}