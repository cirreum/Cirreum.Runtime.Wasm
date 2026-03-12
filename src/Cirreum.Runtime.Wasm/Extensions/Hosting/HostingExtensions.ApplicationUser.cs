namespace Cirreum.Runtime;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides extension methods for configuring application user services in the dependency injection container.
/// </summary>
public static partial class HostingExtensions {

	/// <summary>
	/// Registers the <typeparamref name="TApplicationUserFactory"/> as the
	/// <see cref="IApplicationUserFactory"/> used to load application users during initialization.
	/// </summary>
	/// <typeparam name="TApplicationUserFactory">
	/// The factory implementation that creates <see cref="IApplicationUser"/> instances
	/// from <see cref="IUserState"/>.
	/// </typeparam>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	/// <remarks>
	/// The registered factory is invoked by the <see cref="IInitializationOrchestrator"/>
	/// during Phase 1 of application initialization, after authentication has settled.
	/// </remarks>
	public static IServiceCollection AddApplicationUser<TApplicationUserFactory>(
		this IServiceCollection services)
		where TApplicationUserFactory : class, IApplicationUserFactory {

		services.TryAddScoped<IApplicationUserFactory, TApplicationUserFactory>();
		return services;

	}

	/// <summary>
	/// Registers an <see cref="IApplicationUserFactory"/> using a custom factory function.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <param name="factory">
	/// A factory function that creates an <see cref="IApplicationUserFactory"/> instance
	/// using the service provider.
	/// </param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	/// <remarks>
	/// The registered factory is invoked by the <see cref="IInitializationOrchestrator"/>
	/// during Phase 1 of application initialization, after authentication has settled.
	/// </remarks>
	public static IServiceCollection AddApplicationUser(
		this IServiceCollection services,
		Func<IServiceProvider, IApplicationUserFactory> factory) {

		services.TryAddScoped(factory);
		return services;

	}

}