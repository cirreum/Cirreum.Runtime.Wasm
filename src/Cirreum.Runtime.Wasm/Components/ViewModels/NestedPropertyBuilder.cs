namespace Cirreum.Components.ViewModels;

using System.Linq.Expressions;

/// <summary>
/// Builder for configuring properties of nested ViewModels
/// </summary>
/// <typeparam name="TNestedViewModel">The type of nested ViewModel being configured</typeparam>
public class NestedPropertyBuilder<TNestedViewModel>
	where TNestedViewModel : NestedStateViewModel<TNestedViewModel> {

	// holds both simple and nested properties
	private readonly List<INestedPropertyContext> _properties = [];

	/// <summary>
	/// Expose raw commands for execution.
	/// </summary>
	internal IReadOnlyList<INestedPropertyContext> GetNestedProperties() => this._properties;

	/// <summary>
	/// Configure a simple property using the ViewModelDefaults.
	/// </summary>
	public NestedPropertyBuilder<TNestedViewModel> Property<TProp>(
		Expression<Func<TNestedViewModel, TProp>> propertyExpression)
		where TProp : notnull
		=> this.Property(propertyExpression, ViewModelDefaults.GetDefaultValue<TProp>(), true);

	/// <summary>
	/// Configure a simple property with explicit default.
	/// </summary>
	/// <typeparam name="TProp"></typeparam>
	/// <param name="propertyExpression"></param>
	/// <param name="defaultValue">The default value for the property.</param>
	public NestedPropertyBuilder<TNestedViewModel> Property<TProp>(
		Expression<Func<TNestedViewModel, TProp>> propertyExpression,
		TProp defaultValue)
		where TProp : notnull
		=> this.Property(propertyExpression, defaultValue, true);

	/// <summary>
	/// Configure a simple property with explicit default and integration flag.
	/// </summary>
	/// <param name="propertyExpression"></param>
	/// <param name="defaultValue">The default value for the property.</param>
	/// <param name="integrateWithEditContext">Set to <see langword="false"/> to not communicate state changes to the EditContext. Default: <see langword="true"/></param>
	public NestedPropertyBuilder<TNestedViewModel> Property<TProp>(
		Expression<Func<TNestedViewModel, TProp>> propertyExpression,
		TProp defaultValue,
		bool integrateWithEditContext)
		where TProp : notnull {

		var memberExpr = (MemberExpression)propertyExpression.Body;
		var name = memberExpr.Member.Name;

		// store a typed command, no boxing until execution
		this._properties.Add(new StateViewModelProperty<TProp>(
			name,
			defaultValue,
			integrateWithEditContext
		));

		return this;
	}


	/// <summary>
	/// Configure a nested ViewModel's properties.
	/// </summary>
	/// <typeparam name="TDeepNested">The type of the nested view model, which must inherit from <see cref="NestedStateViewModel{TDeep}"/> and have a
	/// parameterless constructor.</typeparam>
	/// <param name="nestedExpression">An expression that specifies the property of the current view model to associate with the nested group.</param>
	/// <param name="configure">An action to configure the nested group builder for the specified property.</param>
	/// <returns>The current <see cref="NestedPropertyBuilder{TNestedViewModel}"/> instance, allowing for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method allows you to define and configure a nested group of properties for a specific property
	/// of the current view model.
	/// </para>
	/// </remarks>
	public NestedPropertyBuilder<TNestedViewModel> Nested<TDeepNested>(
		Expression<Func<TNestedViewModel, TDeepNested>> nestedExpression,
		Action<NestedPropertyBuilder<TDeepNested>> configure)
		where TDeepNested : NestedStateViewModel<TDeepNested>, new() {

		var memberExpr = (MemberExpression)nestedExpression.Body;
		var groupName = memberExpr.Member.Name;

		// build the nested commands
		var nestedBuilder = new NestedPropertyBuilder<TDeepNested>();
		configure(nestedBuilder);
		var nestedCommands = nestedBuilder.GetNestedProperties();

		// wrap nested commands so they propagate the group prefix
		this._properties.Add(new NestedPropertyContext(groupName, nestedCommands));

		return this;

	}

}