namespace Cirreum.Runtime.Components;

using Cirreum.Components.ViewModels;
using System.Runtime.CompilerServices;

/// <summary>
/// An abstract base component that provides container state management with pluggable storage backends.
/// Extends <see cref="StateComponentBase"/> with automatic state integration and dynamic property creation.
/// </summary>
/// <typeparam name="TStateService">The type of state service interface (IMemoryState, ILocalState, or ISessionState)</typeparam>
/// <remarks>
/// <para>This abstract base class provides:</para>
/// <list type="bullet">
///     <item>
///         <description>Dynamic property creation without explicit configuration</description>
///     </item>
///     <item>
///         <description>Automatic persistence through the injected state service</description>
///     </item>
///     <item>
///         <description><c>Get</c>, <c>Set</c>, and <c>GetOrCreate</c> methods for state management</description>
///     </item>
///     <item>
///         <description>Manages proper lifecycle management to prevent memory leaks</description>
///     </item>
/// </list>
/// </remarks>
public abstract class ContainerStateComponent<TStateService> : StateComponentBase
		where TStateService : class, IStateContainer {

	/// <summary>
	/// The injected state service that provides storage operations.
	/// </summary>
	protected abstract TStateService StateService { get; set; }

	/// <summary>
	/// Gets the namespace prefix used for state persistence to prevent key collisions across applications or tenants.
	/// Override to customize the global namespace for your application context.
	/// </summary>
	/// <returns>The namespace prefix, defaults to the type's namespace or "App" if no namespace is available.</returns>
	/// <remarks>
	/// This prefix is used as the first part of the persistence key hierarchy to ensure state isolation.
	/// By default, uses the actual .NET namespace of the page type (e.g., "MyApp.Features.Counter").
	/// Override this method to provide custom name spacing such as tenant-specific prefixes.
	/// Examples: "MyApp.Features.Counter", "TenantABC", "Module.Feature"
	/// </remarks>
	protected virtual string GetNamespace() => this.GetType().Namespace ?? "App";

	/// <summary>
	/// Gets the scope name used for state key organization within the namespace.
	/// Override to customize scoping behavior for logical grouping of related pages.
	/// </summary>
	/// <returns>The scope name, defaults to the component type name.</returns>
	/// <remarks>
	/// This scope is used as the second part of the persistence key hierarchy for logical organization.
	/// Examples: "UserProfile", "ProductCatalog", "Dashboard"
	/// </remarks>
	protected virtual string GetScope() => this.GetType().Name;

	/// <summary>
	/// Gets a property value from memory state.
	/// </summary>
	/// <typeparam name="T">The type of the property value</typeparam>
	/// <param name="key">The property key</param>
	/// <returns>The current value, or the default value for the type if not found</returns>
	protected T Get<T>([CallerMemberName] string? key = null) where T : notnull {
		var persistedKey = this.BuildPersistedKey(key!);
		this._accessedKeys.Add(persistedKey);
		var (value, _) = this.StateService.GetOrCreate(persistedKey, default(T)!);
		return value;
	}

	/// <summary>
	/// Sets a property value in memory state.
	/// </summary>
	/// <typeparam name="T">The type of the property value</typeparam>
	/// <param name="value">The value to set</param>
	/// <param name="key">The property key</param>
	/// <returns>A task representing the asynchronous operation</returns>
	protected async Task Set<T>(T value, [CallerMemberName] string? key = null) where T : notnull {
		var propertyName = StateViewModelProperty.ResolvePropertyNameFromMethodName(key!);
		var persistedKey = this.BuildPersistedKey(propertyName);
		this._accessedKeys.Add(persistedKey);
		var (_, set) = this.StateService.GetOrCreate(persistedKey, value);
		await set(value);
	}

	/// <summary>
	/// Gets or creates a property with a default value and returns both the current value and a setter function.
	/// </summary>
	/// <typeparam name="T">The type of the property value</typeparam>
	/// <param name="key">The property key</param>
	/// <param name="defaultValue">The default value to use if the property doesn't exist</param>
	/// <returns>A tuple containing the current value and an async setter function</returns>
	protected (T value, Func<T, Task> set) GetOrCreate<T>(string key, T defaultValue) where T : notnull {
		var persistedKey = this.BuildPersistedKey(key);
		this._accessedKeys.Add(persistedKey);
		var (current, _) = this.StateService.GetOrCreate(persistedKey, defaultValue);
		return (current, value => this.Set(value, key));
	}

	/// <summary>
	/// Removes a property from memory state.
	/// </summary>
	/// <param name="key">The property key to remove</param>
	protected void Remove(string key) {
		var persistedKey = this.BuildPersistedKey(key);
		this.StateService.Remove(persistedKey);
		this._accessedKeys.Remove(persistedKey);
	}

	/// <summary>
	/// Removes all properties for this component from memory state.
	/// </summary>
	protected void ClearAll() {
		// Track all keys that have been accessed for this component
		var pageKeys = this._accessedKeys.ToList();
		if (pageKeys.Count > 0) {
			this.StateService.Remove(pageKeys);
			this._accessedKeys.Clear();
		}
	}

	private readonly HashSet<string> _accessedKeys = [];

	/// <summary>
	/// Builds the complete persistence key using the format: "{namespace}:{scope}:{propertyKey}".
	/// This hierarchical structure prevents key collisions and provides logical organization.
	/// </summary>
	/// <param name="key">The property key</param>
	/// <returns>The complete persistence key</returns>
	/// <example>
	/// For a component with namespace="MyApp", scope="UserProfile", and key="firstName":
	/// Result: "MyApp:UserProfile:firstName"
	/// </example>
	private string BuildPersistedKey(string key) => $"{this.GetNamespace()}:{this.GetScope()}:{key.ToLowerInvariant()}";

}