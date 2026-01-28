namespace Cirreum.Runtime;

using Cirreum.Runtime.Authentication.PostProcessors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides extension methods for configuring application user services in the dependency injection container.
/// </summary>
public static partial class HostingExtensions {

	/// <summary>
	/// Registers application user services with the specified user type and loader implementation.
	/// </summary>
	/// <typeparam name="TApplicationUser">The type of the application user that implements <see cref="IApplicationUser"/>.</typeparam>
	/// <typeparam name="TApplicationUserLoader">The type of the application user loader that implements <see cref="IApplicationUserLoader{TApplicationUser}"/>.</typeparam>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddApplicationUser<TApplicationUser, TApplicationUserLoader>(
		this IServiceCollection services)
		where TApplicationUser : class, IApplicationUser
		where TApplicationUserLoader : class, IApplicationUserLoader<TApplicationUser> {

		// Register the loader implementation
		services.TryAddScoped<IApplicationUserLoader<TApplicationUser>, TApplicationUserLoader>();

		// Auto-load after authentication
		services.TryAddEnumerable(
			ServiceDescriptor
				.Scoped<IAuthenticationPostProcessor, ApplicationUserProcessor<TApplicationUser>>());

		return services;

	}

	/// <summary>
	/// Registers application user services with a custom loader factory function.
	/// </summary>
	/// <typeparam name="TApplicationUser">The type of the application user that implements <see cref="IApplicationUser"/>.</typeparam>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <param name="loaderFactory">A factory function that creates an instance of <see cref="IApplicationUserLoader{TApplicationUser}"/> using the service provider.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddApplicationUser<TApplicationUser>(
		this IServiceCollection services,
		Func<IServiceProvider, IApplicationUserLoader<TApplicationUser>> loaderFactory)
		where TApplicationUser : class, IApplicationUser {

		// Register the loader factory
		services.TryAddScoped(loaderFactory);

		// Auto-load after authentication
		services.TryAddEnumerable(
			ServiceDescriptor
				.Scoped<IAuthenticationPostProcessor, ApplicationUserProcessor<TApplicationUser>>());

		return services;

	}

}