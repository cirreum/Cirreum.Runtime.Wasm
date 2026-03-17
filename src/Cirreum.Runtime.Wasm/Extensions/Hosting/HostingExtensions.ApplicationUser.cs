namespace Cirreum.Runtime;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides extension methods for configuring application user services in the dependency injection container.
/// </summary>
public static partial class HostingExtensions {

	/// <summary>
	/// Registers the <typeparamref name="TResolver"/> as the
	/// <see cref="IApplicationUserResolver"/> used to resolve application users during initialization.
	/// </summary>
	/// <typeparam name="TResolver">
	/// The resolver implementation that resolves <see cref="IApplicationUser"/> instances
	/// from an external user identifier.
	/// </typeparam>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	/// <remarks>
	/// The registered resolver is invoked by the <see cref="IInitializationOrchestrator"/>
	/// during Phase 1 of application initialization, after authentication has settled.
	/// </remarks>
	public static IServiceCollection AddApplicationUserResolver<TResolver>(
		this IServiceCollection services)
		where TResolver : class, IApplicationUserResolver {

		services.TryAddScoped<IApplicationUserResolver, TResolver>();
		return services;

	}

	/// <summary>
	/// Registers an <see cref="IApplicationUserResolver"/> using a custom factory function.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <param name="factory">
	/// A factory function that creates an <see cref="IApplicationUserResolver"/> instance
	/// using the service provider.
	/// </param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	/// <remarks>
	/// The registered resolver is invoked by the <see cref="IInitializationOrchestrator"/>
	/// during Phase 1 of application initialization, after authentication has settled.
	/// </remarks>
	public static IServiceCollection AddApplicationUserResolver(
		this IServiceCollection services,
		Func<IServiceProvider, IApplicationUserResolver> factory) {

		services.TryAddScoped(factory);
		return services;

	}

}
