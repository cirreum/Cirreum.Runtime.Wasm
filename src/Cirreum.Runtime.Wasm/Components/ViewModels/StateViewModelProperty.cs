namespace Cirreum.Components.ViewModels;

using Microsoft.AspNetCore.Components.Forms;

internal class StateViewModelProperty {

	internal static string ResolvePropertyNameFromMethodName(string propertyName) {

		// Handle both regular and nested property names
		string actualPropertyName;
		var lastDotIndex = propertyName.LastIndexOf('.');

		if (lastDotIndex >= 0) {
			// Nested property: "HomeAddress.SetStreet" -> "HomeAddress.Street"
			var prefix = propertyName[..lastDotIndex];           // "HomeAddress"
			var suffix = propertyName[(lastDotIndex + 1)..];     // "SetStreet"

			var hasSetPrefix = suffix.StartsWith("Set", StringComparison.Ordinal) &&
							   suffix.Length > 3 &&
							   char.IsUpper(suffix[3]);

			var actualSuffix = hasSetPrefix ? suffix[3..] : suffix; // "Street"
			actualPropertyName = $"{prefix}.{actualSuffix}";        // "HomeAddress.Street"
		} else {
			// Regular property: "SetFirstName" -> "FirstName"
			var hasSetPrefix = propertyName.StartsWith("Set", StringComparison.Ordinal) &&
							   propertyName.Length > 3 &&
							   char.IsUpper(propertyName[3]);
			actualPropertyName = hasSetPrefix ? propertyName[3..] : propertyName;
		}

		return actualPropertyName;

	}

}

/// <summary>
/// Applies a single property configuration of type TValue.
/// </summary>
/// <typeparam name="TValue">The type of the property value.</typeparam>
/// <param name="propertyName">The name of the property being added.</param>
/// <param name="defaultValue">The default value for the property.</param>
/// <param name="integrateWithEditContext">Set to <see langword="false"/> to not communicate state changes to the EditContext. Default: <see langword="true"/></param>
internal sealed class StateViewModelProperty<TValue>(
	string propertyName,
	TValue defaultValue,
	bool integrateWithEditContext
) : StateViewModelProperty, IPropertyContext<TValue> where TValue : notnull {

	private bool _hasBeenAdded = false;

	public string PropertyName { get; private set; } = propertyName;
	public TValue DefaultValue { get; private set; } = defaultValue;
	public bool IntegrateWithEditContext { get; private set; } = integrateWithEditContext;

	public string PersistedKey { get; set; } = "";
	public FieldIdentifier? FieldIdentifier { get; set; }

	public void Add(IPropertyStore props, List<Func<Task>> resetHandlers) {
		this.EnsureNotAdded();
		props.Add(this);
		resetHandlers.Add(() => props.ResetValue(this.PropertyName, this.DefaultValue, markAsUnmodified: false));
		this._hasBeenAdded = true;
	}
	public void AddNested(IPropertyStore props, string parentPropertyName, List<Func<Task>> resetHandlers) {
		this.EnsureNotAdded();
		if (!string.IsNullOrWhiteSpace(parentPropertyName)) {
			this.PropertyName = parentPropertyName + "." + this.PropertyName;
		}
		this.Add(props, resetHandlers);
	}

	public TValue GetValue(IStateContainer state) {
		var (val, _) = state.GetOrCreate(this.PersistedKey, this.DefaultValue);
		return val;
	}

	private void EnsureNotAdded() {
		if (this._hasBeenAdded) {
			throw new InvalidOperationException($"{nameof(StateViewModelProperty<TValue>)} for '{this.PropertyName}' has already been added to a store.");
		}
	}

}