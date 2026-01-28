namespace Cirreum.Components.ViewModels;

using Microsoft.AspNetCore.Components.Forms;

/// <summary>
/// Interface that defines state management capabilities for view models in Blazor applications.
/// </summary>
/// <remarks>
/// <para>
/// This interface serves as the foundation for view models that are backed by a state container
/// and integrate with Blazor's form validation system. It provides a unified interface for
/// property management, validation integration, and supports state persistence for 
/// state containers that inherit from <see cref="IPersistableStateContainer"/>.
/// </para>
/// <para>
/// Key capabilities include:
/// </para>
/// <list type="bullet">
/// <item><description>Property registration and state persistence through configuration methods</description></item>
/// <item><description>Integration with <see cref="EditContext"/> for form validation</description></item>
/// <item><description>Strict property configuration requirement - all properties must be explicitly registered</description></item>
/// <item><description>Notification scoping to coalesce multiple state changes into single notifications</description></item>
/// <item><description>Nested view model support for complex object hierarchies</description></item>
/// </list>
/// <para>
/// All properties must be explicitly configured during view model setup. For non-persistent, 
/// in-memory-only storage, use an <see cref="IMemoryState"/> container instead of 
/// <see cref="ISessionState"/> or <see cref="ILocalState"/> or any state container that 
/// implements <see cref="IStateContainer"/>.
/// </para>
/// <para>
/// Concrete implementations must provide the specific state storage mechanism and implement
/// the required members to define how properties are managed and persisted.
/// </para>
/// </remarks>
public interface IStateViewModel : IViewModel {

	/// <summary>
	/// Clears all registered properties and removes their values from the backing state.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is useful when you do not want the view model properties to remain in state.
	/// </para>
	/// <para>
	/// NOTE: The view model instance is not usable once the properties have been cleared.
	/// </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">Cannot clear properties while the state is being mutated.</exception>
	void Clear();

	/// <summary>
	/// Creates a scope that coalesces multiple state changes into a single notification.
	/// </summary>
	/// <returns>An <see cref="IAsyncDisposable"/> scope that will trigger a single notification when disposed.</returns>
	/// <remarks>
	/// <para>
	/// Supports nested scopes - notifications are deferred until the outermost scope is disposed.
	/// </para>
	/// <para>
	/// Use with a using statement to automatically handle scope disposal:
	/// <code>
	/// await using(var scope = viewModel.CreateNotificationScope()) {
	///     await viewModel.SetFirstName("John");
	///     await viewModel.SetLastName("Doe");
	/// }
	/// </code>
	/// </para>
	/// <para>
	/// This is a wrapper method for the backing state container's notification scope functionality.
	/// </para>
	/// </remarks>
	IAsyncDisposable CreateNotificationScope();

}