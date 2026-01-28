namespace Cirreum.Components.ViewModels;

using Microsoft.AspNetCore.Components.Forms;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

public abstract class NestedStateViewModel<TNestedViewModel>() : IViewModel
	where TNestedViewModel : NestedStateViewModel<TNestedViewModel> {

	private StateViewModel? _parentViewModel;
	private string? _parentKey;

	/// <inheritdoc/>
	[JsonIgnore]
	public EditContext EditContext =>
		_parentViewModel?.EditContext
		?? throw new InvalidOperationException("NestedViewModel not initialized");

	/// <summary>
	/// Not used in nested view models, as they do not manage their own state directly.
	/// </summary>
	/// <returns></returns>
	public Task Reset() {
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public FieldIdentifier? GetFieldIdentifier(string propertyName) {
		var nestedPropertyName = $"{_parentKey}.{propertyName}";
		return _parentViewModel?.GetFieldIdentifier(nestedPropertyName);
	}


	internal void Initialize(StateViewModel parentViewModel, string parentKey) {
		_parentViewModel = parentViewModel;
		_parentKey = parentKey;
	}



	/// <summary>
	/// Gets a property value from state.
	/// </summary>
	/// <typeparam name="TProp">The type of the property.</typeparam>
	/// <param name="propertyName">The name of the property (automatically provided by the compiler).</param>
	/// <returns>The current value of the property from state, or its default value if not found.</returns>
	protected TProp Get<TProp>([CallerMemberName] string? propertyName = null) where TProp : notnull {
		if (_parentViewModel is null) {
			return ViewModelDefaults.GetDefaultValue<TProp>();
		}
		var nestedPropertyName = $"{_parentKey}.{propertyName}";
		return _parentViewModel.Get<TProp>(nestedPropertyName);
	}

	/// <summary>
	/// Sets a property's value in state.
	/// </summary>
	/// <typeparam name="TProp">The type of the property.</typeparam>
	/// <param name="value">The new value to persist in state.</param>
	/// <param name="propertyName">The name of the property (automatically provided by the compiler).</param>
	/// <returns>A task representing the asynchronous set operation.</returns>
	/// <remarks>
	/// <para>
	/// Updates the value in state and triggers a state change notification to any subscribers.
	/// </para>
	/// <para>
	/// When setting multiple property values at once, use the parent <see cref="StateViewModel.CreateNotificationScope"/> to only send a
	/// single notification when the scope completes.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">The <paramref name="propertyName"/> is null, empty or white-space.</exception>
	protected Task Set<TProp>(TProp value, [CallerMemberName] string? propertyName = null) where TProp : notnull {
		if (_parentViewModel is null) {
			return Task.CompletedTask;
		}
		var nestedPropertyName = $"{_parentKey}.{propertyName}";
		return _parentViewModel.Set(value, nestedPropertyName);
	}

	/// <summary>
	/// Creates or retrieves a cached instance of a nested ViewModel.
	/// </summary>
	protected TDeepNested GetNested<TDeepNested>([CallerMemberName] string? propertyName = null)
			where TDeepNested : NestedStateViewModel<TDeepNested>, new() {
		if (_parentViewModel is null) {
			throw new InvalidOperationException("NestedViewModel not initialized. Ensure Initialize() is called before accessing nested ViewModels.");
		}
		var nestedPropertyName = $"{_parentKey}.{propertyName}";
		return (TDeepNested)_parentViewModel.NestedViewModels.GetOrAdd(nestedPropertyName, _ => {
			var nested = new TDeepNested();
			nested.Initialize(_parentViewModel, nestedPropertyName);
			return nested;
		});
	}

}