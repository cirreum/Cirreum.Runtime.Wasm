namespace Cirreum.Runtime;

using Cirreum.RemoteServices;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.DependencyInjection;

public static partial class HostingExtensions {

	static readonly Dictionary<string, (IHttpClientBuilder, RemoteServiceOptions)> NamedBuilders =
		new(StringComparer.OrdinalIgnoreCase);


	/// <summary>
	/// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/>
	/// and configures a binding between the <typeparamref name="TClient"/> and a named <see cref="HttpClient"/>.
	/// The client name will be set to the type name of <typeparamref name="TClient"/>.
	/// </summary>
	/// <typeparam name="TClient"></typeparam>
	/// <param name="builder">The current <see cref="IClientDomainApplicationBuilder"/></param>
	/// <param name="configure">A delegate that is used to configure the <see cref="RemoteServiceOptions"/>.</param>
	/// <param name="clientName">
	/// The logical name of the <see cref="HttpClient"/> to configure.
	/// Defaults to the type name of <typeparamref name="TClient"/>.
	/// </param>
	/// <param name="applicationName">The name of the application calling the remote service.</param>
	/// <returns>The provided <see cref="IClientDomainApplicationBuilder"/></returns>
	/// <remarks>
	/// <see cref="HttpClient"/> instances that apply the provided configuration can
	/// be retrieved using <see cref="IHttpClientFactory.CreateClient(string)"/>
	/// and providing the matching name.
	///
	/// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient"/>
	/// can be retrieved from <see cref="IServiceProvider.GetService(Type)"/> (and related
	/// methods) by providing <typeparamref name="TClient"/> as the service type.
	/// </remarks>
	public static IClientDomainApplicationBuilder AddRemoteClient<TClient>(
		this IClientDomainApplicationBuilder builder,
		Action<RemoteServiceOptions> configure,
		string? clientName = null,
		string? applicationName = null)
		where TClient : class {

		ArgumentNullException.ThrowIfNull(builder);
		clientName ??= typeof(TClient).Name;

		RemoteServiceOptions options = applicationName is null ? new() : new(applicationName);
		configure.Invoke(options);

		builder.Services
			.AddRemoteServiceHttpClient(clientName, options)
			.AddTypedClient<TClient>();

		return builder;

	}

	/// <summary>
	/// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/>
	/// and configures a binding between the <typeparamref name="TClient"/> and a named <see cref="HttpClient"/>.
	/// The client name will be set to the type name of <typeparamref name="TClient"/>.
	/// </summary>
	/// <typeparam name="TClient"></typeparam>
	/// <param name="builder">The current <see cref="IClientDomainApplicationBuilder"/></param>
	/// <param name="configure">A delegate that is used to configure the <see cref="RemoteServiceOptions"/>.</param>
	/// <param name="clientName">
	/// The logical name of the <see cref="HttpClient"/> to configure.
	/// Defaults to the type name of <typeparamref name="TClient"/>.
	/// </param>
	/// <returns>The provided <see cref="IClientDomainApplicationBuilder"/></returns>
	/// <remarks>
	/// <see cref="HttpClient"/> instances that apply the provided configuration can
	/// be retrieved using <see cref="IHttpClientFactory.CreateClient(string)"/>
	/// and providing the matching name.
	///
	/// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient"/>
	/// can be retrieved from <see cref="IServiceProvider.GetService(Type)"/> (and related
	/// methods) by providing <typeparamref name="TClient"/> as the service type.
	/// </remarks>
	public static IClientDomainApplicationBuilder AddRemoteClient<TClient>(
		this IClientDomainApplicationBuilder builder,
		Action<RemoteServiceOptions> configure,
		string? clientName = null)
		where TClient : class {

		ArgumentNullException.ThrowIfNull(builder);
		clientName ??= typeof(TClient).Name;

		RemoteServiceOptions options = new();
		configure.Invoke(options);

		builder.Services
			.AddRemoteServiceHttpClient(clientName, options)
			.AddTypedClient<TClient>();

		return builder;

	}

	/// <summary>
	/// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/>
	/// and configures a binding between the <typeparamref name="TClient"/> and a named <see cref="HttpClient"/>.
	/// The client name will be set to the type name of <typeparamref name="TClient"/>.
	/// </summary>
	/// <typeparam name="TClient"></typeparam>
	/// <param name="builder">The current <see cref="IClientDomainApplicationBuilder"/></param>
	/// <param name="options">The <see cref="RemoteServiceOptions"/>.</param>
	/// <param name="clientName">
	/// The optional name of the <see cref="HttpClient"/> to configure.
	/// Defaults to the type name of <typeparamref name="TClient"/>.
	/// </param>
	/// <returns>The provided <see cref="IClientDomainApplicationBuilder"/></returns>
	/// <remarks>
	/// <see cref="HttpClient"/> instances that apply the provided configuration can
	/// be retrieved using <see cref="IHttpClientFactory.CreateClient(string)"/>
	/// and providing the matching name.
	///
	/// <typeparamref name="TClient"/> instances constructed with the appropriate <see cref="HttpClient"/>
	/// can be retrieved from <see cref="IServiceProvider.GetService(Type)"/> (and related
	/// methods) by providing <typeparamref name="TClient"/> as the service type.
	/// </remarks>
	public static IClientDomainApplicationBuilder AddRemoteClient<TClient>(
		this IClientDomainApplicationBuilder builder,
		RemoteServiceOptions options,
		string? clientName = null)
		where TClient : class {

		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(options);
		clientName ??= typeof(TClient).Name;

		builder.Services
			.AddRemoteServiceHttpClient(clientName, options)
			.AddTypedClient<TClient>();

		return builder;

	}

	/// <summary>
	/// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/>
	/// and configures a named <see cref="HttpClient"/>.
	/// </summary>
	/// <param name="builder">The current <see cref="IClientDomainApplicationBuilder"/></param>
	/// <param name="clientName">
	/// The logical name of the <see cref="HttpClient"/> to configure.
	/// </param>
	/// <param name="configure">
	/// A delegate that is used to configure the <see cref="RemoteServiceOptions"/>.
	/// </param>
	/// <returns>The provided <see cref="IClientDomainApplicationBuilder"/></returns>
	/// <remarks>
	/// <see cref="HttpClient"/> instances that apply the provided configuration can
	/// be retrieved using <see cref="IHttpClientFactory.CreateClient(string)"/>
	/// and providing the matching name.
	/// </remarks>
	public static IClientDomainApplicationBuilder AddRemoteClient(
		this IClientDomainApplicationBuilder builder,
		string clientName,
		Action<RemoteServiceOptions> configure) {

		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);
		ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

		RemoteServiceOptions options = new();
		configure.Invoke(options);

		builder.Services
			.AddRemoteServiceHttpClient(clientName, options);

		return builder;

	}

	/// <summary>
	/// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/>
	/// and configures a named <see cref="HttpClient"/>.
	/// </summary>
	/// <param name="builder">The current <see cref="IClientDomainApplicationBuilder"/></param>
	/// <param name="applicationName">The name of the application calling the remote service.</param>
	/// <param name="clientName">
	/// The logical name of the <see cref="HttpClient"/> to configure.
	/// </param>
	/// <param name="configure">
	/// A delegate that is used to configure the <see cref="RemoteServiceOptions"/>.
	/// </param>
	/// <returns>The provided <see cref="IClientDomainApplicationBuilder"/></returns>
	/// <remarks>
	/// <see cref="HttpClient"/> instances that apply the provided configuration can
	/// be retrieved using <see cref="IHttpClientFactory.CreateClient(string)"/>
	/// and providing the matching name.
	/// </remarks>
	public static IClientDomainApplicationBuilder AddRemoteClient(
		this IClientDomainApplicationBuilder builder,
		string applicationName,
		string clientName,
		Action<RemoteServiceOptions> configure) {

		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);
		ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
		ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

		RemoteServiceOptions options = new(applicationName);
		configure.Invoke(options);

		builder.Services
			.AddRemoteServiceHttpClient(clientName, options);

		return builder;

	}

	/// <summary>
	/// Adds the <see cref="IHttpClientFactory"/> and related services to the <see cref="IServiceCollection"/>
	/// and configures a named <see cref="HttpClient"/>.
	/// </summary>
	/// <param name="builder">The current <see cref="IClientDomainApplicationBuilder"/></param>
	/// <param name="clientName">
	/// The logical name of the <see cref="HttpClient"/> to configure.
	/// </param>
	/// <param name="options">
	/// The <see cref="RemoteServiceOptions"/> instance.
	/// </param>
	/// <returns>The provided <see cref="IClientDomainApplicationBuilder"/></returns>
	/// <remarks>
	/// <see cref="HttpClient"/> instances that apply the provided configuration can
	/// be retrieved using <see cref="IHttpClientFactory.CreateClient(string)"/>
	/// and providing the matching name.
	/// </remarks>
	public static IClientDomainApplicationBuilder AddRemoteClient(
		this IClientDomainApplicationBuilder builder,
		string clientName,
		RemoteServiceOptions options) {

		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

		builder.Services
			.AddRemoteServiceHttpClient(clientName, options);

		return builder;

	}


	private static IHttpClientBuilder AddRemoteServiceHttpClient(
		this IServiceCollection services,
		string clientName,
		RemoteServiceOptions options) {

		if (string.IsNullOrWhiteSpace(options.ServiceUri.OriginalString)) {
			throw new InvalidOperationException("Missing the 'ServiceUri'.");
		}
		if (!options.ServiceUri.IsAbsoluteUri) {
			throw new InvalidOperationException($"ServiceUri must be an absolute URI. Unsupported: {options.ServiceUri}");
		}

		if (NamedBuilders.TryGetValue(clientName, out var value)) {
			(var registeredHttpBuilder, var registeredOptions) = value;
			if (registeredOptions.Equals(options)) {
				// no need to re-register the same named instance with the same options
				return registeredHttpBuilder;
			}
			throw new Exception("Cannot register an HttpClient with the same Name and different options.");
		}

		// check for optional pre-configured Authorization Header
		if (options.AuthorizationHeader is { HasValue: true }) {

			var namedBuilderWithToken = services.AddHttpClient(clientName, (client) => {
				client.BaseAddress = options.ServiceUri;
				if (options.ApplicationName.HasValue()) {
					client.DefaultRequestHeaders.Add(RemoteIdentityConstants.AppNameHeader, options.ApplicationName);
				}
				client.DefaultRequestHeaders.Authorization =
					new System.Net.Http.Headers.AuthenticationHeaderValue(
						options.AuthorizationHeader.Scheme,
						options.AuthorizationHeader.Value);
			});

			NamedBuilders.Add(clientName, (namedBuilderWithToken, options));

			return namedBuilderWithToken;

		}

		// Explicit no auth (public API)
		if (options.AuthorizationHeader is not null) {
			var namedBuilderNoAuth = services.AddHttpClient(clientName, (client) => {
				client.BaseAddress = options.ServiceUri;
				if (options.ApplicationName.HasValue()) {
					client.DefaultRequestHeaders.Add(RemoteIdentityConstants.AppNameHeader, options.ApplicationName);
				}
			});

			NamedBuilders.Add(clientName, (namedBuilderNoAuth, options));
			return namedBuilderNoAuth;
		}

		// Default: OIDC/OAuth via AuthorizationMessageHandler
		var namedBuilderWithAuth = services
			.AddHttpClient(clientName, (client) => {
				client.BaseAddress = options.ServiceUri;
				if (options.ApplicationName.HasValue()) {
					client.DefaultRequestHeaders.Add(RemoteIdentityConstants.AppNameHeader, options.ApplicationName);
				}
			})
			.RedactLoggedHeaders(options.RedactedHeaders)
			.AddHttpMessageHandler(sp => {
				return sp.GetRequiredService<AuthorizationMessageHandler>()
					.ConfigureHandler(
						authorizedUrls: [options.ServiceUri.OriginalString],
						scopes: options.ServiceScopes);
			});

		NamedBuilders.Add(clientName, (namedBuilderWithAuth, options));

		return namedBuilderWithAuth;

	}

}