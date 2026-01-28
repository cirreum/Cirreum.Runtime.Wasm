namespace Cirreum.Components.ViewModels;

/// <summary>
/// Provides default value creation for common types used in ViewModels
/// </summary>
public static class ViewModelDefaults {

	private static readonly Dictionary<Type, Func<object>> DefaultValueFactories = new() {
		{ typeof(string), () => string.Empty },
		{ typeof(string[]), () => Array.Empty<string>() },
		{ typeof(int[]), () => Array.Empty<int>() },
		{ typeof(bool[]), () => Array.Empty<bool>() },
		{ typeof(DateTime[]), () => Array.Empty<DateTime>() },
		{ typeof(decimal[]), () => Array.Empty<decimal>() },
		{ typeof(List<string>), () => new List<string>() },
		{ typeof(List<int>), () => new List<int>() },
		{ typeof(List<bool>), () => new List<bool>() },
		{ typeof(List<DateTime>), () => new List<DateTime>() },
		{ typeof(List<decimal>), () => new List<decimal>() },
		{ typeof(Dictionary<string, string>), () => new Dictionary<string, string>() },
		{ typeof(Dictionary<string, object>), () => new Dictionary<string, object>() },
		{ typeof(Dictionary<string, int>), () => new Dictionary<string, int>() },
		{ typeof(Guid), () => Guid.NewGuid() }
	};

	/// <summary>
	/// Creates a default value for the specified type
	/// </summary>
	/// <typeparam name="T">The type to create a default value for</typeparam>
	/// <returns>A default value appropriate for the type</returns>
	/// <exception cref="InvalidOperationException">Thrown when no default can be created for the type</exception>
	public static T GetDefaultValue<T>() where T : notnull {
		return GetDefaultValue<T>(typeof(T));
	}

	/// <summary>
	/// Creates a default value for the specified type, ensuring it matches the generic type parameter
	/// </summary>
	/// <typeparam name="T">The expected return type</typeparam>
	/// <param name="type">The type to create a default value for (must be assignable to T)</param>
	/// <returns>A default value appropriate for the type</returns>
	/// <exception cref="ArgumentException">Thrown when type is not assignable to T</exception>
	/// <exception cref="InvalidOperationException">Thrown when no default can be created for the type</exception>
	public static T GetDefaultValue<T>(Type type) {

		// Validate compatibility
		if (!typeof(T).IsAssignableFrom(type)) {
			throw new ArgumentException($"Type {type.Name} is not assignable to {typeof(T).Name}", nameof(type));
		}

		var result = GetDefaultValue(type);
		return (T)result;
	}

	/// <summary>
	/// Creates a default value for the specified type
	/// </summary>
	/// <param name="type">The type to create a default value for</param>
	/// <returns>A default value appropriate for the type</returns>
	/// <exception cref="InvalidOperationException">Thrown when no default can be created for the type</exception>
	public static object GetDefaultValue(Type type) {

		// Handle value types (int, bool, DateTime, etc.)
		if (type.IsValueType) {
			return Activator.CreateInstance(type)!;
		}

		// Check our factory dictionary first
		if (DefaultValueFactories.TryGetValue(type, out var factory)) {
			return factory();
		}

		// Handle arrays generically
		if (type.IsArray) {
			var elementType = type.GetElementType()!;
			var emptyArray = Array.CreateInstance(elementType, 0);
			return emptyArray;
		}

		// Handle generic collections with parameterless constructors
		if (type.IsGenericType && type.GetConstructor(Type.EmptyTypes) != null) {
			return Activator.CreateInstance(type)!;
		}

		// Handle classes with parameterless constructors
		if (type.IsClass && type.GetConstructor(Type.EmptyTypes) != null) {
			return Activator.CreateInstance(type)!;
		}

		// Fallback for unsupported types
		throw new InvalidOperationException(
			$"Cannot create default value for type {type.Name}. " +
			$"Supported types: value types, string, arrays, List<T>, Dictionary<TKey,TValue>, " +
			$"and classes with parameterless constructors. " +
			$"Consider providing an explicit default value in your configuration.");
	}

	/// <summary>
	/// Registers a custom default value factory for a specific type
	/// </summary>
	/// <typeparam name="T">The type to register a factory for</typeparam>
	/// <param name="factory">Function that creates the default value</param>
	public static void RegisterDefaultValueFactory<T>(Func<T> factory) where T : notnull {
		DefaultValueFactories[typeof(T)] = () => factory()!;
	}

	/// <summary>
	/// Checks if a default value can be created for the specified type
	/// </summary>
	/// <typeparam name="T">The type to check</typeparam>
	/// <returns>True if a default value can be created, false otherwise</returns>
	public static bool CanCreateDefaultValue<T>() where T : notnull {
		var type = typeof(T);

		return type.IsValueType ||
			   DefaultValueFactories.ContainsKey(type) ||
			   type.IsArray ||
			   (type.IsGenericType && type.GetConstructor(Type.EmptyTypes) != null) ||
			   (type.IsClass && type.GetConstructor(Type.EmptyTypes) != null);
	}

}