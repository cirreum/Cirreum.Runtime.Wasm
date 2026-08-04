namespace Cirreum.Runtime;

using Cirreum.RemoteServices;
using Cirreum.Runtime.Security;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring the application user in the dependency injection container.
/// </summary>
public static partial class HostingExtensions {

	/// <summary>
	/// Registers the app's application-user type and the service that hosts the framework
	/// bootstrap endpoint. During initialization the framework fetches the caller's own
	/// application user from <c>GET /_cirreum/application-user</c> on
	/// <paramref name="serviceUri"/> — replacing the client-side
	/// <c>IApplicationUserResolver</c> an app previously wrote and registered itself.
	/// </summary>
	/// <typeparam name="TUser">
	/// The app's <see cref="IApplicationUser"/> implementation — the type the server-side
	/// resolver returns, which the endpoint serializes against its runtime type.
	/// </typeparam>
	/// <param name="builder">The <see cref="IClientDomainApplicationBuilder"/>.</param>
	/// <param name="serviceUri">
	/// The base URI of the Cirreum server hosting the app's domain (the same service the
	/// app's remote clients call). The framework owns the route; the app supplies only what
	/// is genuinely its own — the type and the service.
	/// </param>
	/// <returns>The provided <see cref="IClientDomainApplicationBuilder"/>.</returns>
	/// <remarks>
	/// <para>
	/// The call rides the standard remote-service pipeline: the default
	/// <c>AuthorizationMessageHandler</c> attaches the caller's access token using the app's
	/// configured default scopes, exactly as any other remote client for the same service.
	/// </para>
	/// <para>
	/// The bootstrap endpoint requires authentication and nothing else — it is never
	/// dispatched through the server's operation pipeline — so the record arrives even for a
	/// disabled caller, letting the router render the disabled experience instead of falling
	/// through to "not provisioned".
	/// </para>
	/// <para>
	/// Cirreum WASM hosts follow the <strong>single-IdP-client invariant</strong>: calling
	/// this method twice throws rather than silently overriding the first registration.
	/// </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Thrown when an application user has already been registered.
	/// </exception>
	public static IClientDomainApplicationBuilder AddApplicationUser<TUser>(
		this IClientDomainApplicationBuilder builder,
		Uri serviceUri)
		where TUser : class, IApplicationUser {

		ArgumentNullException.ThrowIfNull(builder);
		builder.Services.AddApplicationUser<TUser>(serviceUri);
		return builder;

	}

	/// <summary>
	/// Registers the app's application-user type and the service that hosts the framework
	/// bootstrap endpoint. The <see cref="IServiceCollection"/>-level core of
	/// <see cref="AddApplicationUser{TUser}(IClientDomainApplicationBuilder, Uri)"/> —
	/// the surface the IdP composition wrappers (Msal / Oidc) forward to.
	/// </summary>
	/// <typeparam name="TUser">
	/// The app's <see cref="IApplicationUser"/> implementation — the type the server-side
	/// resolver returns, which the endpoint serializes against its runtime type.
	/// </typeparam>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <param name="serviceUri">
	/// The base URI of the Cirreum server hosting the app's domain.
	/// </param>
	/// <returns>The provided <see cref="IServiceCollection"/>.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when an application user has already been registered.
	/// </exception>
	public static IServiceCollection AddApplicationUser<TUser>(
		this IServiceCollection services,
		Uri serviceUri)
		where TUser : class, IApplicationUser {

		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(serviceUri);

		ThrowIfApplicationUserAlreadyRegistered(services);

		// AuthorizationHeader stays null so the default OIDC/OAuth token-acquisition branch
		// wires the AuthorizationMessageHandler — the endpoint requires authentication.
		var options = new RemoteServiceOptions {
			ServiceUri = serviceUri
		};

		services
			.AddRemoteServiceHttpClient(ApplicationUserClient.ClientName, options)
			.AddTypedClient<ApplicationUserClient, InternalApplicationUserClient<TUser>>();

		return services;

	}

	private static void ThrowIfApplicationUserAlreadyRegistered(IServiceCollection services) {
		if (services.Any(d => d.ServiceType == typeof(ApplicationUserClient))) {
			throw new InvalidOperationException(
				"AddApplicationUser has already been called. Cirreum WASM hosts follow the " +
				"single-IdP-client invariant — each client binds to one IdP and one " +
				"application-user type, so the application user may only be registered once.");
		}
	}

}
