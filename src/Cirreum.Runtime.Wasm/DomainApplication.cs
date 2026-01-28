namespace Cirreum.Runtime;

/// <summary>
/// Provides factory methods for creating domain application builders.
/// </summary>
public static class DomainApplication {

	/// <summary>
	/// Creates and configures a new <see cref="DomainApplicationBuilder"/> instance with default settings.
	/// </summary>
	/// <param name="args">Command line arguments passed to the application.</param>
	/// <returns>
	/// A configured <see cref="DomainApplicationBuilder"/> instance ready for further customization.
	/// </returns>
	/// <remarks>
	/// This factory method creates a builder with reasonable defaults for Blazor WebAssembly applications.
	/// This builder extends the standard WebAssemblyHostBuilder with additional features
	/// for domain-driven client applications. It configures core services, authentication state,
	/// and client-specific infrastructure.
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
	/// builder.Services.AddScoped&lt;IUserProfile, UserProfile&gt;();
	/// 
	/// // Run the application
	/// await builder.BuildAndRunAsync&lt;MyDomainType&gt;();
	/// </code>
	/// </example>
	public static DomainApplicationBuilder CreateBuilder(
		string[] args) {
		return DomainApplicationBuilder.CreateAndConfigureBuilder(
			args);
	}

}