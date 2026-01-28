namespace Cirreum.Runtime;

using Cirreum.Conductor.Configuration;
using Cirreum.Runtime.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// A builder for Blazor WebAssembly applications that integrates domain services,
/// authorization, validation, and CQRS features.
/// </summary>
/// <remarks>
/// <para>
/// This builder extends the standard WebAssemblyHostBuilder with additional features
/// for domain-driven client applications. It configures core services, authentication state,
/// and client-specific infrastructure.
/// </para>
/// <para>
/// Use the <see cref="DomainApplication.CreateBuilder(string[])"/>
/// method to create a pre-configured instance of this builder. Then, configure additional services and
/// root components as needed before calling one of the <see cref="BuildAndRunAsync()"/> methods.
/// </para>
/// <para>
/// The builder supports specifying additional assemblies containing domain services,
/// validators, and authorization handlers through the <see cref="DomainServicesBuilder"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var builder = DomainApplication.CreateBuilder(args);
/// 
/// builder.RootComponents.Add&lt;HeadOutlet&gt;("head::after");
/// builder.RootComponents.Add&lt;App&gt;("#app");
/// 
/// // services
/// builder.Services.AddScoped&lt;IDataService, ApiDataService&gt;();
/// builder.Services.AddScoped&lt;IUserProfileService, UserProfileService&gt;();
/// 
/// // Run the application
/// await builder.BuildAndRunAsync&lt;MyDomainType&gt;();
/// </code>
/// </example>
public sealed class DomainApplicationBuilder : IClientDomainApplicationBuilder {

	internal static DomainApplicationBuilder CreateAndConfigureBuilder(string[] args) {

		var webAppBuilder = new DomainApplicationBuilder(WebAssemblyHostBuilder.CreateDefault(args));

		webAppBuilder.Services.AddSingleton<IDomainEnvironment, DomainEnvironment>();

		return webAppBuilder;

	}

	private readonly WebAssemblyHostBuilder _innerBuilder;
	private Action<ConductorOptionsBuilder>? _conductorConfiguration;

	/// <inheritdoc/>
	public ILoggingBuilder Logging => this._innerBuilder.Logging;

	/// <inheritdoc/>
	public IServiceCollection Services => this._innerBuilder.Services;


	/// <summary>
	/// Gets the collection of root component mappings configured for the application.
	/// </summary>
	public RootComponentMappingCollection RootComponents => this._innerBuilder.RootComponents;

	/// <summary>
	/// Gets an <see cref="WebAssemblyHostConfiguration"/> that can be used to customize the application's
	/// configuration sources and read configuration attributes.
	/// </summary>
	public WebAssemblyHostConfiguration Configuration => this._innerBuilder.Configuration;

	/// <summary>
	/// Gets information about the app's host environment.
	/// </summary>
	public IWebAssemblyHostEnvironment HostEnvironment => this._innerBuilder.HostEnvironment;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="innerBuilder"></param>
	private DomainApplicationBuilder(WebAssemblyHostBuilder innerBuilder) {
		this._innerBuilder = innerBuilder;
	}

	/// <inheritdoc/>
	public IDomainApplicationBuilder ConfigureConductor(Action<ConductorOptionsBuilder> configure) {
		ArgumentNullException.ThrowIfNull(configure);

		var previousConfig = _conductorConfiguration;
		_conductorConfiguration = options => {
			previousConfig?.Invoke(options);
			configure(options);
		};

		return this;
	}

	/// <summary>
	/// Builds and Runs the application after registering and configuring domain services including
	/// authorization evaluators, documenters, and CQRS features.
	/// </summary>
	/// <returns>A <see cref="Task"/> which represents exit of the application.</returns>
	/// <remarks>
	/// This method registers core domain services without specifying additional assemblies
	/// that should be scanned. If your domain services are in separate assemblies, consider using 
	/// the overloads that allow you to specify assemblies to scan.
	/// </remarks>
	public async Task BuildAndRunAsync() {

		// ******************************************************************************
		// Build the Domain
		//
		var domain = this.BuildDomainCore();

		// ******************************************************************************
		// Initialize the application
		//
		await domain.Services.InitializeApplicationAsync();

		// ******************************************************************************
		// Run the application
		//
		await domain.RunAsync();

	}

	/// <summary>
	/// Builds and Runs the application after registering and configuring domain services with custom
	/// assembly configuration.
	/// </summary>
	/// <param name="configureDomainServices">A callback to configure domain service assemblies.</param>
	/// <returns>A <see cref="Task"/> which represents exit of the application.</returns>
	/// <remarks>
	/// This method allows you to specify additional assemblies that should be scanned for domain services,
	/// validators, and authorization handlers. Use the provided <see cref="DomainServicesBuilder"/>
	/// to register assemblies containing your domain components.
	/// </remarks>
	/// <example>
	/// <code>
	/// await builder.RunAsync(domain => {
	///     domain.AddAssemblyContaining&lt;Asm1.GetOrders&gt;()
	///           .AddAssemblyContaining&lt;Asm2.GetUsers&gt;();
	/// });
	/// </code>
	/// </example>
	public async Task BuildAndRunAsync(Action<DomainServicesBuilder> configureDomainServices) {

		// Build domain services if any...
		var domainBuilder = new DomainServicesBuilder();
		configureDomainServices(domainBuilder);

		// ******************************************************************************
		// Run the application
		//
		await this.BuildAndRunAsync();

	}

	/// <summary>
	/// Builds and Runs the application after registering and configuring domain services, including
	/// the assembly containing the specified marker type.
	/// </summary>
	/// <typeparam name="TDomainMarker">A type from the assembly containing domain services to register.</typeparam>
	/// <returns>A <see cref="Task"/> which represents exit of the application.</returns>
	/// <remarks>
	/// This is a convenience method that allows you to include an additional assembly containing the specified type.
	/// Use this method when your domain services are in a single separate assembly from your API.
	/// </remarks>
	/// <example>
	/// <code>
	/// await builder.RunAsync&lt;SomeDomainType&gt;();
	/// </code>
	/// </example>
	public Task BuildAndRunAsync<TDomainMarker>() {
		return this.BuildAndRunAsync(domain => domain.AddAssemblyContaining<TDomainMarker>());
	}

	private WebAssemblyHost BuildDomainCore() {

		// ******************************************************************************
		// Ensure we have a default authorization provider
		//
		if (!this.Services.Any(d => d.ServiceType == typeof(AuthenticationStateProvider))) {
			this.AddDefaultAuthorization();
		}

		// ******************************************************************************
		// Core (Infrastructure) Services
		//
		this.Services
			.AddCoreServices();


		// ******************************************************************************
		// IUserState
		//
		var clientUser = new ClientUser(); // enforce no dependencies
		this.Services
			.AddSingleton(clientUser)
			.AddSingleton<IUserState>(clientUser);

		// ******************************************************************************
		// Themeing Components
		//
		this.Services
			.AddScoped<IThemeStateManager, DefaultThemeStateManager>()
			.AddScoped<IThemeModeIconProvider, DefaultThemeModeIconProvider>();

		// ******************************************************************************
		// Runtime Services
		//
		this.Services

			// Authentication State
			.AddCascadingAuthenticationState()

			// IJSAppModule
			.AddSingleton<JSAppModule>()
			.AddSingleton<IJSAppModule>(provider => provider.GetRequiredService<JSAppModule>())
			.AddSingleton<IBrowserCultureInterop>(provider => provider.GetRequiredService<JSAppModule>())

			// Core Blazor Components Library
			.AddCoreComponents()

			// Initializers
			.AddApplicationInitializers(ServiceLifetime.Scoped)

			// Default Telemetry User Context
			.TryAddSingleton<ITelemetryUserContext, NullTelemetryUserContext>();


		// ******************************************************************************
		// Domain Authorization Related Services
		//
		this.Services.AddDefaultAuthorizationEvaluator();
		this.Services.AddDefaultAuthorizationDocumenter(ServiceLifetime.Scoped);


		// ******************************************************************************
		// App Domain - Conductor/FluentValidation/FluentAuthorization
		//
		this.Services.AddDomainServices(this.Configuration, _conductorConfiguration);


		// ******************************************************************************
		// Build the app!
		//
		return this._innerBuilder.Build();

	}

}