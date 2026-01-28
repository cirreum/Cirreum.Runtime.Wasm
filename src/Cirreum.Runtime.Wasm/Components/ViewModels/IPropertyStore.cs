namespace Cirreum.Components.ViewModels;

using Microsoft.AspNetCore.Components.Forms;

/// <summary>
/// Defines a view models collection of properties.
/// </summary>
/// <remarks>
/// Maps view state to a view model.
/// </remarks>
internal interface IPropertyStore {

	/// <summary>
	/// Retrieves the <see cref="FieldIdentifier"/> associated with the specified property name.
	/// </summary>
	/// <param name="propertyName">The name of the property for which to retrieve the field identifier.</param>
	/// <returns>The <see cref="FieldIdentifier"/> associated with the specified property name, or <see langword="null"/>  if the
	/// property name does not exist in the current context.</returns>
	FieldIdentifier? GetFieldIdentifier(string propertyName);

	/// <summary>
	/// Adds a property context to the property store, registering it for state management and EditContext integration.
	/// </summary>
	/// <typeparam name="TProp">The type of the property value. Must be a non-nullable type.</typeparam>
	/// <param name="context">The property context containing the configuration and metadata for the property being added. Cannot be null.</param>
	/// <remarks>
	/// <para>
	/// This method registers a strongly-typed property context with the store, enabling state persistence,
	/// validation integration, and field tracking. The context contains all necessary metadata including
	/// the property name, default value, and EditContext integration settings.
	/// </para>
	/// <para>
	/// Once added, the property can be accessed through <see cref="GetValue{TProp}(string)"/> and modified
	/// through <see cref="SetValue{TProp}(string, TProp)"/> using the property name specified in the context.
	/// </para>
	/// <para>
	/// This method is typically called during the view model configuration phase and should not be called
	/// after the view model has been fully configured.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when a property with the same name has already been added to the store.</exception>
	void Add<TProp>(IPropertyContext<TProp> context) where TProp : notnull;

	/// <summary>
	/// Retrieves the value of a property from the state.
	/// </summary>
	/// <typeparam name="TProp">The type of the property value.</typeparam>
	/// <param name="propertyName">The name of the property to retrieve.</param>
	/// <returns>The current value of the property.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the property has not been registered.</exception>
	TProp GetValue<TProp>(string propertyName) where TProp : notnull;

	/// <summary>
	/// Set the value of the property in state to its default value.
	/// </summary>
	/// <typeparam name="TProp">The type of the property value.</typeparam>
	/// <param name="propertyName">The name of the property to set.</param>
	/// <param name="defaultValue">The new value to set.</param>
	/// <returns>A task representing the asynchronous set operation.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the property has not been registered.</exception>
	Task ResetValue<TProp>(string propertyName, TProp defaultValue) where TProp : notnull;

	/// <summary>
	/// Resets the specified property to its default value and optionally marks it as unmodified.
	/// </summary>
	/// <remarks>This method resets the persisted state of the specified property to the provided default value. If
	/// <paramref name="markAsUnmodified"/> is <see langword="true"/>, the property will also be marked as unmodified in
	/// the associated edit context, ensuring it is treated as unchanged.</remarks>
	/// <typeparam name="TProp">The type of the property value. Must be a non-nullable type.</typeparam>
	/// <param name="propertyName">The name of the property to reset. Cannot be null or empty.</param>
	/// <param name="defaultValue">The default value to assign to the property. Must be non-null.</param>
	/// <param name="markAsUnmodified">A value indicating whether the property should be marked as unmodified in the associated edit context. If <see
	/// langword="true"/>, the property will be marked as unmodified.</param>
	/// <returns>A completed task representing the asynchronous operation.</returns>
	Task ResetValue<TProp>(string propertyName, TProp defaultValue, bool markAsUnmodified) where TProp : notnull;

	/// <summary>
	/// Updates the EditContext reference and rebuilds FieldIdentifiers for properties.
	/// </summary>
	/// <param name="newEditContext">The new EditContext to use.</param>
	void ResetContext(EditContext newEditContext);

	/// <summary>
	/// Sets the value of the property in state.
	/// </summary>
	/// <typeparam name="TProp">The type of the property value.</typeparam>
	/// <param name="propertyName">The name of the property to set.</param>
	/// <param name="value">The new value to set.</param>
	/// <returns>A task representing the asynchronous set operation.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the property has not been registered.</exception>
	Task SetValue<TProp>(string propertyName, TProp value) where TProp : notnull;

	/// <summary>
	/// Removes all registered properties for this view model from the state.
	/// </summary>
	void Clear();

}