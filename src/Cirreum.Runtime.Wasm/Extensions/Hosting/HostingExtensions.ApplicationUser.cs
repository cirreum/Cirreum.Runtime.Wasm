namespace Cirreum.Runtime;

using Microsoft.Extensions.DependencyInjection;

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
	/// <para>
	/// Cirreum WASM hosts follow the <strong>single-IdP-client invariant</strong> — each
	/// WASM client binds to exactly one IdP, so only one <see cref="IApplicationUserResolver"/>
	/// may be registered. Calling this method twice throws
	/// <see cref="InvalidOperationException"/> rather than silently overriding the first
	/// registration or accumulating duplicates.
	/// </para>
	/// <para>
	/// On the server side, multi-IdP hosting is the common case and the matching
	/// registration extension on <c>CirreumAuthorizationBuilder</c> permits N resolvers
	/// dispatched by scheme. WASM-side dispatch is structurally a no-op (one candidate);
	/// see <c>InitializationOrchestrator</c> for the dispatch logic and rationale.
	/// </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Thrown when an <see cref="IApplicationUserResolver"/> has already been registered.
	/// </exception>
	public static IServiceCollection AddApplicationUserResolver<TResolver>(
		this IServiceCollection services)
		where TResolver : class, IApplicationUserResolver {

		ThrowIfResolverAlreadyRegistered(services);
		services.AddScoped<IApplicationUserResolver, TResolver>();
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
	/// Cirreum WASM hosts follow the single-IdP-client invariant — only one
	/// <see cref="IApplicationUserResolver"/> may be registered per host. Calling this
	/// method when a resolver is already registered (via either overload) throws
	/// <see cref="InvalidOperationException"/>.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Thrown when an <see cref="IApplicationUserResolver"/> has already been registered.
	/// </exception>
	public static IServiceCollection AddApplicationUserResolver(
		this IServiceCollection services,
		Func<IServiceProvider, IApplicationUserResolver> factory) {

		ThrowIfResolverAlreadyRegistered(services);
		services.AddScoped(factory);
		return services;

	}

	private static void ThrowIfResolverAlreadyRegistered(IServiceCollection services) {
		if (services.Any(d => d.ServiceType == typeof(IApplicationUserResolver))) {
			throw new InvalidOperationException(
				"An IApplicationUserResolver is already registered. Cirreum WASM hosts " +
				"follow the single-IdP-client invariant — each WASM client binds to one " +
				"IdP, so only one resolver may be registered. If a host genuinely needs " +
				"multi-IdP dispatch on the client side, set o.UserOptions.AuthenticationType " +
				"during AddOidcAuthentication / AddMsalAuthentication and compose dispatch " +
				"inside a single resolver implementation.");
		}
	}

}
