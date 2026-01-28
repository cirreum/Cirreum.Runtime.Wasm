namespace Cirreum.Components.ViewModels;

/// <summary>
/// Contracts a configuration that can apply itself against an IPropertyStore.
/// </summary>
internal interface INestedPropertyContext {
	/// <summary>
	/// Add this nested property to the specified property store.
	/// </summary>
	/// <param name="props">Target property store.</param>
	/// <param name="parentPropertyName">The name of the property that is the parent of this property.</param>
	/// <param name="resetHandlers">List to collect reset actions.</param>
	void AddNested(IPropertyStore props, string parentPropertyName, List<Func<Task>> resetHandlers);
}