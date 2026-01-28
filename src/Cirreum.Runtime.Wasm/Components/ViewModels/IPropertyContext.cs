namespace Cirreum.Components.ViewModels;

using Microsoft.AspNetCore.Components.Forms;

/// <summary>
/// Represents the context for a property, providing metadata and functionality for managing property state and
/// behavior.
/// </summary>
/// <remarks>This interface is typically used to define the context of a property within a component or system, 
/// including its name, persisted key, and integration with an edit context. It also supports associating  the property
/// with a field identifier for validation or tracking purposes.</remarks>
internal interface IPropertyContext : INestedPropertyContext {
	/// <summary>
	/// Gets or sets the persisted key used for identifying or securing resources.
	/// </summary>
	string PersistedKey { get; set; }
	/// <summary>
	/// Gets the name of the property as a string.
	/// </summary>
	string PropertyName { get; }
	/// <summary>
	/// Gets or sets the identifier for the field associated with this instance.
	/// </summary>
	FieldIdentifier? FieldIdentifier { get; set; }
	/// <summary>
	/// Gets a value indicating whether the component integrates with the EditContext for form validation and state
	/// management.
	/// </summary>
	bool IntegrateWithEditContext { get; }
	/// <summary>
	/// Add this property to the specified property store.
	/// </summary>
	/// <remarks>This method updates the provided property store with the specified values and ensures that the
	/// reset handlers are properly registered. The reset handlers are executed asynchronously and should be designed to
	/// revert the applied changes when invoked.</remarks>
	/// <param name="props">The property store to which the property values will be applied. Cannot be null.</param>
	/// <param name="resetHandlers">A list of asynchronous reset handlers to be registered. Each handler represents a task to reset specific
	/// properties. Cannot be null.</param>
	void Add(IPropertyStore props, List<Func<Task>> resetHandlers);
}

/// <summary>
/// Represents a context for accessing a property value of type <typeparamref name="TProp"/>.
/// </summary>
/// <remarks>This interface provides a mechanism to retrieve the value of a property from a given state container.
/// It is intended for use in scenarios where property values are dynamically resolved based on state.</remarks>
/// <typeparam name="TProp">The type of the property value.</typeparam>
internal interface IPropertyContext<TProp> : IPropertyContext {
	/// <summary>
	/// Retrieves the value of the specified property from the given state container.
	/// </summary>
	/// <param name="state">The state container from which the property value is retrieved. Cannot be null.</param>
	/// <returns>The value of the property as specified by the state container.</returns>
	TProp GetValue(IStateContainer state);
}