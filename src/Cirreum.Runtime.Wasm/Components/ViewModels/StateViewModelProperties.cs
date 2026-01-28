namespace Cirreum.Components.ViewModels;

using Microsoft.AspNetCore.Components.Forms;

internal sealed class StateViewModelProperties<TViewModel>(
	string scope,
	IStateContainer state,
	EditContext editContext
) : IPropertyStore
	where TViewModel : IViewModel {

	private static readonly string _viewTypeName = typeof(TViewModel).Name;

	// Registry of property context instances
	private readonly Dictionary<string, IPropertyContext> _properties = [];
	private IPropertyContext GetPropertyContext(string propertyName) {
		if (!this._properties.TryGetValue(propertyName, out var context)) {
			throw new InvalidOperationException($"Property '{propertyName}' not found.");
		}
		return context;
	}

	/// <inheritdoc/>
	public FieldIdentifier? GetFieldIdentifier(string propertyName) {
		if (!this._properties.TryGetValue(propertyName, out var context)) {
			return null;
		}
		return context.FieldIdentifier;
	}

	public void Add<TProp>(IPropertyContext<TProp> context) where TProp : notnull {
		var persistedKey = $"{scope}:{_viewTypeName}:{context.PropertyName}";
		context.PersistedKey = persistedKey;
		context.FieldIdentifier = context.IntegrateWithEditContext ? editContext.Field(context.PropertyName) : null;
		this._properties[context.PropertyName] = context;
	}

	/// <inheritdoc/>
	public TProp GetValue<TProp>(string propertyName) where TProp : notnull {
		var context = this.GetPropertyContext(propertyName);
		if (context is IPropertyContext<TProp> typedContext) {
			return typedContext.GetValue(state);
		}
		throw new InvalidOperationException($"Property '{propertyName}' is not of type '{typeof(TProp).Name}'.");
	}

	/// <inheritdoc/>
	public Task ResetValue<TProp>(string propertyName, TProp defaultValue) where TProp : notnull
		=> this.ResetValue(propertyName, defaultValue, markAsUnmodified: true);

	/// <inheritdoc/>
	public Task ResetValue<TProp>(string propertyName, TProp defaultValue, bool markAsUnmodified) where TProp : notnull {
		var context = this.GetPropertyContext(propertyName);

		state.Reset(context.PersistedKey, defaultValue);

		if (markAsUnmodified && context.FieldIdentifier.HasValue) {
			editContext.MarkAsUnmodified(context.FieldIdentifier.Value);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public void ResetContext(EditContext newEditContext) {

		// Replace the reference
		editContext = newEditContext;

		// Rebuild all FieldIdentifiers with the new context
		foreach (var context in this._properties.Values) {
			if (context.IntegrateWithEditContext) {
				var newFieldIdentifier = newEditContext.Field(context.PropertyName);
				this._properties[context.PropertyName].FieldIdentifier = newFieldIdentifier;
			}
		}
	}

	/// <inheritdoc/>
	public async Task SetValue<TProp>(string propertyName, TProp value) where TProp : notnull {
		var context = this.GetPropertyContext(propertyName);
		var (_, set) = state.GetOrCreate(context.PersistedKey, value);
		await set(value);
		if (context.FieldIdentifier.HasValue) {
			editContext.NotifyFieldChanged(context.FieldIdentifier.Value); // Triggers field validation
		}
	}

	/// <inheritdoc/>
	public void Clear() {
		editContext.MarkAsUnmodified();
		foreach (var context in this._properties.Values) {
			if (context.FieldIdentifier.HasValue) {
				editContext.Properties.Remove(context.FieldIdentifier.Value);
			}
		}
		var persistedKeys = this._properties.Values.Select(s => s.PersistedKey).ToList();
		state.Remove(persistedKeys);
		this._properties.Clear();
		editContext.NotifyValidationStateChanged();
	}

}