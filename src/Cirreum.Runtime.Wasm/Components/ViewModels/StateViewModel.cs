namespace Cirreum.Components.ViewModels;

using Cirreum.Components.Validation;
using Microsoft.AspNetCore.Components.Forms;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

/// <summary>
/// Internal abstract base class that provides state management capabilities for view models in Blazor applications.
/// </summary>
/// <remarks>
/// <para>
/// This class serves as the foundation for the state view model framework and should not be inherited from directly.
/// Instead, inherit from one of the concrete implementations such as <c>SessionStateViewModel&lt;TViewModel&gt;</c>,
/// <c>LocalStateViewModel&lt;TViewModel&gt;</c>, or <c>MemoryStateViewModel&lt;TViewModel&gt;</c>, or create a custom
/// implementation by inheriting from <see cref="StateViewModel{TViewModel, TState}"/>.
/// </para>
/// <para>
/// This base class defines the common interface and shared functionality for all state-managed view models,
/// including:
/// </para>
/// <list type="bullet">
/// <item><description>Property registration and state persistence infrastructure</description></item>
/// <item><description>Integration with <see cref="EditContext"/> for form validation</description></item>
/// <item><description>Strict property configuration requirement - all properties must be explicitly registered</description></item>
/// <item><description>Notification scoping to coalesce multiple state changes into single notifications</description></item>
/// <item><description>Nested view model support for complex object hierarchies</description></item>
/// </list>
/// <para>
/// All properties accessed through the framework must be explicitly configured during the configuration phase.
/// For different persistence behaviors, choose the appropriate concrete implementation based on your storage needs.
/// </para>
/// </remarks>
/// <seealso cref="StateViewModel{TViewModel, TState}"/>
public abstract class StateViewModel : IStateViewModel {

	/// <inheritdoc/>
	[JsonIgnore]
	public abstract EditContext EditContext { get; }
	/// <inheritdoc/>
	public abstract Task Reset();
	/// <inheritdoc/>
	public abstract FieldIdentifier? GetFieldIdentifier(string propertyName);
	/// <inheritdoc/>
	public abstract void Clear();
	/// <inheritdoc/>
	public abstract IAsyncDisposable CreateNotificationScope();

	[JsonIgnore]
	internal readonly ConcurrentDictionary<string, object> NestedViewModels = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Called during construction to create the properties for the view, which will be persisted.
	/// </summary>
	protected abstract void Configure();

	/// <summary>
	/// Gets a property value from state.
	/// </summary>
	/// <typeparam name="TProp">The type of the property.</typeparam>
	/// <param name="propertyName">The name of the property (automatically provided by the compiler).</param>
	/// <returns>The current value of the configured property from state.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the property has not been registered using <see cref="Configure"/>.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the ViewModel itself has not been configured.</exception>
	protected internal abstract TProp Get<TProp>(string propertyName) where TProp : notnull;

	/// <summary>
	/// Sets a property's value in state.
	/// </summary>
	/// <typeparam name="TProp">The type of the property.</typeparam>
	/// <param name="value">The new value to set.</param>
	/// <param name="propertyName">The name of the property (automatically provided by the compiler).</param>
	/// <returns>A task representing the asynchronous set operation.</returns>
	/// <exception cref="ArgumentException">The <paramref name="propertyName"/> is null, empty or white-space.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the ViewModel itself has not been configured.</exception>
	protected internal abstract Task Set<TProp>(TProp value, string propertyName) where TProp : notnull;

}

/// <summary>
/// Generic base implementation of <see cref="StateViewModel"/> that provides concrete state management
/// functionality with pluggable state storage backends for framework implementers.
/// </summary>
/// <typeparam name="TViewModel">The concrete view model type that inherits from this class</typeparam>
/// <typeparam name="TState">The state container type that handles persistence operations</typeparam>
/// <remarks>
/// <para>
/// This class is primarily intended for framework implementers who need to create custom state container
/// integrations. For most application development scenarios, use the concrete implementations such as
/// <c>SessionStateViewModel&lt;TViewModel&gt;</c>, <c>LocalStateViewModel&lt;TViewModel&gt;</c>, or 
/// <c>MemoryStateViewModel&lt;TViewModel&gt;</c> instead.
/// </para>
/// <para>
/// This class implements a sophisticated state management system with strict property configuration
/// requirements, nested view models, and integration with Blazor's validation framework.
/// </para>
/// 
/// <para><strong>When to inherit from this class:</strong></para>
/// <list type="bullet">
/// <item><description>Creating a custom state container integration</description></item>
/// <item><description>Building framework-level abstractions</description></item>
/// <item><description>Implementing specialized persistence behaviors</description></item>
/// </list>
/// 
/// <para><strong>For application development, use these instead:</strong></para>
/// <list type="bullet">
/// <item><description><c>SessionStateViewModel&lt;TViewModel&gt;</c> - Browser session storage</description></item>
/// <item><description><c>LocalStateViewModel&lt;TViewModel&gt;</c> - Browser local storage</description></item>
/// <item><description><c>MemoryStateViewModel&lt;TViewModel&gt;</c> - In-memory only storage</description></item>
/// </list>
/// <para>
/// The nested ViewModel can only access configured properties that have been explicitly registered.
/// Attempting to access unconfigured properties will throw an <see cref="InvalidOperationException"/>.
/// </para>
/// 
/// </remarks>
/// <example>
/// <para><strong>Basic Usage:</strong></para>
/// <code>
/// public class UserViewModel : SessionStateViewModel&lt;UserViewModel&gt; {
///     // Property accessors
///     public string FirstName => this.Get&lt;string&gt;();
///     public Task SetFirstName(string value) => this.Set(value);
///     
///     public string LastName => this.Get&lt;string&gt;();
///     public Task SetLastName(string value) => this.Set(value);
///     
///     // Nested view model
///     public AddressViewModel HomeAddress => this.GetNested&lt;AddressViewModel&gt;();
///     
///     protected override void Configure() => this
///         .CreateProperty(x => x.FirstName, "John")
///         .CreateProperty(x => x.LastName, "Doe")
///         .CreateNested(x => x.HomeAddress, address => address
///             .Property(a => a.Street)
///             .Property(a => a.City)
///             .Property(a => a.State, "CA")
///         );
/// }
/// </code>
/// 
/// <para><strong>Blazor Component Usage:</strong></para>
/// <code>
/// &lt;EditForm Model="viewModel" OnValidSubmit="HandleSubmit"&gt;
///     &lt;DataAnnotationsValidator /&gt;
///     
///     &lt;InputText @bind-Value:get="viewModel.FirstName" 
///                @bind-Value:set="viewModel.SetFirstName" /&gt;
///     &lt;ValidationMessage For="() =&gt; viewModel.FirstName" /&gt;
///     
///     &lt;InputText @bind-Value:get="viewModel.HomeAddress.Street" 
///                @bind-Value:set="viewModel.HomeAddress.SetStreet" /&gt;
///     &lt;ValidationMessage For="() =&gt; viewModel.HomeAddress.Street" /&gt;
///     
///     &lt;button type="submit"&gt;Save&lt;/button&gt;
/// &lt;/EditForm&gt;
/// </code>
/// 
/// <para><strong>Bulk Updates with Notification Scoping:</strong></para>
/// <code>
/// await using (var scope = viewModel.CreateNotificationScope()) {
///     await viewModel.SetFirstName("Jane");
///     await viewModel.SetLastName("Smith");
///     await viewModel.HomeAddress.SetCity("San Francisco");
///     // Single notification sent when scope disposes
/// }
/// </code>
/// </example>
public abstract class StateViewModel<TViewModel, TState> : StateViewModel
	where TViewModel : StateViewModel<TViewModel, TState>
	where TState : IStateContainer {

	private readonly StateViewModelProperties<TViewModel> _properties;
	private readonly List<Func<Task>> _resetHandlers = [];
	private readonly bool _configured;
	private readonly TState _state;
	private readonly string _scope;
	private EditContext _editContext;
	private bool _isMutating;

	/// <summary>
	/// Constructs a new view model instance.
	/// </summary>
	/// <param name="state">The state storage service.</param>
	/// <param name="scope">The name in which the data will be scoped to.</param>
	protected StateViewModel(TState state, string scope) {
		this._state = state;
		this._scope = scope;
		this._editContext = new EditContext(this);
		this._properties = new StateViewModelProperties<TViewModel>(scope, state, this._editContext);
		this.Configure();
		this._configured = true;
	}

	/// <summary>
	/// Gets the current state instance backing this view model.
	/// </summary>
	[JsonIgnore]
	public TState State => this._state;

	/// <summary>
	/// Gets the name in which the data will be scoped to, which determines the prefix for the
	/// property names when persisted in state storage.
	/// </summary>
	[JsonIgnore]
	public string Scope => this._scope;

	/// <summary>
	/// The current <see cref="Microsoft.AspNetCore.Components.Forms.EditContext"/> for this view model
	/// for use with an <see cref="EditForm"/>.
	/// </summary>
	[JsonIgnore]
	public override EditContext EditContext => this._editContext;


	/// <inheritdoc/>
	public override IAsyncDisposable CreateNotificationScope() {
		this.EnsureConfigured();
		return this._state.CreateNotificationScopeAsync();
	}

	/// <inheritdoc/>
	public override async Task Reset() {
		this.EnsureConfigured();
		if (this._isMutating) {
			return;
		}
		try {
			this._isMutating = true;

			await using (var scope = this._state.CreateNotificationScopeAsync()) {
				foreach (var resetFunc in this._resetHandlers) {
					await resetFunc();
				}
			}

			// Create a new EditContext to fully reset validation state
			this._editContext = new EditContext(this);

			// Update the StateViewModelProperties with the new EditContext
			this._properties.ResetContext(this._editContext);

		} finally {
			this._isMutating = false;
		}
	}

	/// <inheritdoc/>
	public override void Clear() {
		if (!this._configured) {
			return;
		}
		if (this._isMutating) {
			throw new InvalidOperationException($"Cannot clear properties while their state is being mutated.");
		}
		this._properties.Clear();
		this._resetHandlers.Clear();
		this._editContext = new EditContext(this);
	}

	/// <inheritdoc/>
	public override FieldIdentifier? GetFieldIdentifier(string propertyName)
		=> this._properties.GetFieldIdentifier(propertyName);

	/// <summary>
	/// Creates dynamic property getter and setter methods, leveraging <typeparamref name="TState"/> and
	/// adds a <see cref="FieldIdentifier"/> to the <see cref="EditContext"/> for ease of use with <c>Forms</c>
	/// (see <see cref="Microsoft.AspNetCore.Components.Forms.EditContext"/>).
	/// </summary>
	/// <typeparam name="TProp">The type of the property.</typeparam>
	/// <param name="expression">An expression that specifies the property to add.</param>
	/// <param name="defaultValue">The default value to use when the property is not found in local state.</param>
	/// <param name="integrateWithEditContext">Set to <see langword="false"/> to not communicate state changes to the EditContext. Default: <see langword="true"/></param>
	/// <returns>This <typeparamref name="TViewModel"/> instance for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// The provided <paramref name="expression"/> must be the get accessor:
	/// <code>
	/// protected override void Configure() =&gt; this
	///		.CreateProperty(x => x.UserName, defaultValue: "Guest")
	///		.CreateProperty(x => x.LastName, defaultValue: "User")
	/// </code>
	/// </para>
	/// <para>
	/// If the component that will render this property is a Blazor <see cref="InputBase{TValue}"/> based component
	/// (e.g., <see cref="InputDate{TValue}"/>) etc., then <paramref name="integrateWithEditContext"/> should be set
	/// to <see langword="false"/> as the Blazor input components already manage notifying the context about value changes.
	/// </para>
	/// <para>
	/// Calling this after <see cref="StateViewModel.Configure"/> has completed will throw an
	/// <c>InvalidOperationException</c>.
	/// </para>
	/// <para>
	/// When defining your concrete property accessors in your view, it is recommended to have a readonly Get and
	/// a dedicated Set method:
	/// <code>
	/// public string UserName => this.Get&lt;string&gt;();
	/// public Task SetUserName(string value) => this.Set(value);
	/// </code>
	/// This is optimized for and works well with Blazor's binding feature:
	/// <code>
	/// &lt;input &#64;bind:get="viewModel.UserName" &#64;bind:set="viewModel.SetUserName" /&gt;
	/// </code>
	/// Also, see the <see cref="InvalidMessage{TValue}"/> component for rendering validation failures as it also
	/// integrates with this object and the <see cref="FluentValidationValidator"/>
	/// <code>
	/// &lt;InvalidMessage For="() =&gt; viewModel.UserName" /&gt;
	/// </code>
	/// </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">Cannot add a property once the view has been configured.</exception>
	/// <exception cref="ArgumentException">Thrown when the expression is not a valid property access.</exception>
	protected TViewModel CreateProperty<TProp>(Expression<Func<TViewModel, TProp>> expression, TProp defaultValue, bool integrateWithEditContext = true) where TProp : notnull {
		this.EnsureNotConfigured("Cannot add a property once the view model has been configured.");

		var memberExpression =
			expression.Body as MemberExpression ??
			throw new ArgumentException("Expression must be a member access", nameof(expression));
		var propertyName = memberExpression.Member.Name;

		var context = new StateViewModelProperty<TProp>(
			propertyName,
			defaultValue,
			integrateWithEditContext
		);
		context.Add(this._properties, this._resetHandlers);
		return (TViewModel)this;
	}

	/// <summary>
	/// Configures a nested ViewModel group by registering its properties for state persistence and creating the necessary infrastructure for nested object serialization.
	/// </summary>
	/// <typeparam name="TNestedViewModel">The type of nested ViewModel to configure, must inherit from <see cref="NestedStateViewModel{TNestedViewModel}"/></typeparam>
	/// <param name="groupExpression">An expression that specifies the nested ViewModel property to configure</param>
	/// <param name="configure">A builder action that defines which properties of the nested ViewModel should be persisted</param>
	/// <returns>This <typeparamref name="TViewModel"/> instance for method chaining</returns>
	/// <remarks>
	/// <para>
	/// This method serves two critical purposes:
	/// </para>
	/// <list type="number">
	/// <item><description><strong>Property Registration:</strong> Registers individual nested properties for state persistence using flat keys (e.g., "HomeAddress.Street")</description></item>
	/// <item><description><strong>Serialization Setup:</strong> Creates a composite handle that groups the flat properties into nested JSON objects during serialization</description></item>
	/// </list>
	/// <para>
	/// Only properties configured through this method will be persisted to state. Unconfigured properties on the nested ViewModel
	/// will work in memory but won't survive application restarts or navigation.
	/// </para>
	/// <para>
	/// The builder pattern allows you to selectively choose which properties to persist, enabling scenarios where
	/// the same nested ViewModel type can have different persistence policies in different contexts.
	/// </para>
	/// <para>
	/// Calling this after <see cref="StateViewModel.Configure"/> has completed will throw an <see cref="InvalidOperationException"/>.
	/// </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">Thrown when called after the ViewModel has been configured</exception>
	/// <exception cref="ArgumentException">Thrown when the groupExpression is not a valid property access</exception>
	/// <example>
	/// <code>
	/// protected override void Configure() => this
	///     .CreateProperty(x => x.FirstName, "John")
	///     
	///     // Configure HomeAddress with full address information
	///     .CreateNested(x => x.HomeAddress, address => address
	///         .Property(a => a.Street)           // Persisted with automatic "" default
	///         .Property(a => a.City)             // Persisted with automatic "" default
	///         .Property(a => a.State, "MA")      // Persisted with explicit default
	///         .Property(a => a.PostalCode)       // Persisted with automatic "" default
	///         .Property(a => a.ApartmentNumber)  // Persisted with automatic "" default
	///     )
	///     
	///     // Configure WorkAddress with minimal information
	///     .CreateNested(x => x.WorkAddress, address => address
	///         .Property(a => a.Street)           // Only these properties are persisted
	///         .Property(a => a.City)             // ApartmentNumber works in memory only
	///     );
	/// 
	/// // Resulting JSON structure:
	/// // {
	/// //   "FirstName": "John",
	/// //   "HomeAddress": {
	/// //     "Street": "", "City": "", "State": "MA", "PostalCode": "", "ApartmentNumber": ""
	/// //   },
	/// //   "WorkAddress": {
	/// //     "Street": "", "City": ""
	/// //   }
	/// // }
	/// </code>
	/// </example>
	protected TViewModel CreateNested<TNestedViewModel>(
		Expression<Func<TViewModel, TNestedViewModel>> groupExpression,
		Action<NestedPropertyBuilder<TNestedViewModel>> configure)
		where TNestedViewModel : NestedStateViewModel<TNestedViewModel>, new() {
		this.EnsureNotConfigured("Cannot add a nested property once the view model has been configured.");

		var memberExpression = groupExpression.Body as MemberExpression
			?? throw new ArgumentException("Expression must be a member access", nameof(groupExpression));
		var propertyName = memberExpression.Member.Name;

		// Create builder to collect nested properties
		var builder = new NestedPropertyBuilder<TNestedViewModel>();
		configure(builder);

		foreach (var property in builder.GetNestedProperties()) {
			property.AddNested(this._properties, propertyName, this._resetHandlers);
		}

		return (TViewModel)this;

	}

	/// <inheritdoc/>
	protected internal override async Task Set<TProp>(TProp value, [CallerMemberName] string? methodName = null) {
		this.EnsureConfigured();
		if (this._isMutating) {
			return;
		}
		try {
			this._isMutating = true;
			var actualPropertyName = StateViewModelProperty.ResolvePropertyNameFromMethodName(methodName!);
			await this._properties.SetValue(actualPropertyName, value);
		} finally {
			this._isMutating = false;
		}
	}

	/// <inheritdoc/>
	protected internal override TProp Get<TProp>([CallerMemberName] string? propertyName = null) {
		this.EnsureConfigured();
		var val = this._properties.GetValue<TProp>(propertyName!);
		return val;
	}

	/// <summary>
	/// Creates or retrieves a cached instance of a nested ViewModel that provides a view over a subset of this ViewModel's state.
	/// </summary>
	/// <typeparam name="TNestedViewModel">The type of nested ViewModel to create, must inherit from <see cref="NestedStateViewModel{TNestedViewModel}"/></typeparam>
	/// <param name="propertyName">The name of the property (automatically provided by the compiler via <see cref="CallerMemberNameAttribute"/>)</param>
	/// <returns>A nested ViewModel instance that is connected to this parent's state and EditContext</returns>
	/// <remarks>
	/// <para>
	/// This method implements a lazy-loading pattern where nested ViewModels are created only when first accessed
	/// and then cached for subsequent calls. Each nested ViewModel is properly initialized with:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Access to the parent's state management system</description></item>
	/// <item><description>Shared EditContext for validation integration</description></item>
	/// <item><description>Scoped property keys to avoid naming conflicts</description></item>
	/// </list>
	/// <para>
	/// The nested ViewModel can access both configured properties (which persist to state) and unconfigured 
	/// properties (which exist only in memory during the session).
	/// </para>
	/// <para>
	/// This method is typically called automatically when accessing nested ViewModel properties:
	/// </para>
	/// <code>
	/// // Property definition - GetNested is called automatically
	/// public AddressViewModel HomeAddress => this.GetNested&lt;AddressViewModel&gt;();
	/// 
	/// // Usage in Razor - triggers GetNested on first access
	/// &lt;input @bind="viewModel.HomeAddress.Street" /&gt;
	/// </code>
	/// </remarks>
	/// <exception cref="InvalidOperationException">Thrown when the ViewModel has not been configured</exception>
	/// <example>
	/// <code>
	/// public class UserViewModel : SessionStateViewModel&lt;UserViewModel&gt; {
	///     // Nested ViewModel properties
	///     public AddressViewModel HomeAddress => this.GetNested&lt;AddressViewModel&gt;();
	///     public AddressViewModel WorkAddress => this.GetNested&lt;AddressViewModel&gt;();
	///     
	///     protected override void Configure() => this
	///         .CreateProperty(x => x.FirstName, "John")
	///         .CreateNested(x => x.HomeAddress, address => address
	///             .Property(a => a.Street)
	///             .Property(a => a.City)
	///         );
	/// }
	/// 
	/// // Usage
	/// var user = new UserViewModel(sessionState, "user");
	/// var addr1 = user.HomeAddress;  // Creates and caches AddressViewModel
	/// var addr2 = user.HomeAddress;  // Returns same cached instance
	/// </code>
	/// </example>
	protected TNestedViewModel GetNested<TNestedViewModel>([CallerMemberName] string? propertyName = null)
		where TNestedViewModel : NestedStateViewModel<TNestedViewModel>, new() {
		this.EnsureConfigured();
		return (TNestedViewModel)NestedViewModels.GetOrAdd(propertyName!, _ => {
			var nested = new TNestedViewModel();
			nested.Initialize(this, propertyName!);
			return nested;
		});
	}

	private void EnsureConfigured() {
		if (!this._configured) {
			throw new InvalidOperationException("ViewModel has not been configured.");
		}
	}
	private void EnsureNotConfigured(string errorMessage) {
		if (this._configured) {
			throw new InvalidOperationException(errorMessage);
		}
	}

}