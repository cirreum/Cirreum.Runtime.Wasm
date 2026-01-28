namespace Cirreum.Components.ViewModels;

/// <summary>
/// Wraps a set of commands under a nested prefix.
/// </summary>
internal sealed class NestedPropertyContext(
	string propertyName,
	IReadOnlyList<INestedPropertyContext> children
) : INestedPropertyContext {

	private readonly string _propertyName = propertyName;

	public void AddNested(IPropertyStore props, string parentPropertyName, List<Func<Task>> resetHandlers) {
		var fullPropertyPath = parentPropertyName + "." + this._propertyName;
		foreach (var cmd in children) {
			cmd.AddNested(props, fullPropertyPath, resetHandlers);
		}
	}

}