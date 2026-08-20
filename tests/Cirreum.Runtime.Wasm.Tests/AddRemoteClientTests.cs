namespace Cirreum.Runtime.Wasm.Tests;

using Cirreum.RemoteServices;
using Cirreum.Runtime;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Guards the interface-typed remote-client registration:
/// <c>AddRemoteClient&lt;TClient, TImplementation&gt;</c> resolves the service type to the
/// concrete client, and — the decision worth locking — names the underlying
/// <see cref="HttpClient"/> after the <em>implementation</em>. Defaulting to the service type
/// would rename the client the moment an app moved from a concrete registration to an
/// interface-typed one, silently detaching any configuration keyed on the old name.
/// </summary>
public sealed class AddRemoteClientTests {

	private static readonly Uri ServiceUri = new("https://api.example.com/");

	private interface IOrdersApi {
		HttpClient Client { get; }
	}

	private sealed class OrdersApiClient(HttpClient client) : IOrdersApi {
		public HttpClient Client { get; } = client;
	}

	private sealed class StubNavigationManager : Microsoft.AspNetCore.Components.NavigationManager {
		public StubNavigationManager() {
			this.Initialize("http://localhost/", "http://localhost/");
		}
		protected override void NavigateToCore(string uri, bool forceLoad) { }
	}

	private sealed class StubAccessTokenProvider : Microsoft.AspNetCore.Components.WebAssembly.Authentication.IAccessTokenProvider {
		public ValueTask<Microsoft.AspNetCore.Components.WebAssembly.Authentication.AccessTokenResult> RequestAccessToken() =>
			throw new NotSupportedException("Token acquisition is not exercised by these tests.");
		public ValueTask<Microsoft.AspNetCore.Components.WebAssembly.Authentication.AccessTokenResult> RequestAccessToken(
			Microsoft.AspNetCore.Components.WebAssembly.Authentication.AccessTokenRequestOptions options) =>
			throw new NotSupportedException("Token acquisition is not exercised by these tests.");
	}

	private static IClientDomainApplicationBuilder BuilderWith(IServiceCollection services) {
		services.AddLogging();
		// The remote-service branch wires AuthorizationMessageHandler into the named client's
		// pipeline; a real app gets it from AddOidcAuthentication / AddMsalAuthentication.
		services.AddScoped(_ =>
			new Microsoft.AspNetCore.Components.WebAssembly.Authentication.AuthorizationMessageHandler(
				new StubAccessTokenProvider(), new StubNavigationManager()));

		var builder = Substitute.For<IClientDomainApplicationBuilder>();
		builder.Services.Returns(services);
		return builder;
	}

	[Fact]
	public void The_service_type_resolves_to_the_implementation() {
		var services = new ServiceCollection();
		var builder = BuilderWith(services);

		builder.AddRemoteClient<IOrdersApi, OrdersApiClient>(o => o.ServiceUri = ServiceUri);

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IOrdersApi>().Should().BeOfType<OrdersApiClient>();
	}

	[Fact]
	public void The_client_name_defaults_to_the_implementation_not_the_service_type() {
		var services = new ServiceCollection();
		var builder = BuilderWith(services);

		builder.AddRemoteClient<IOrdersApi, OrdersApiClient>(o => o.ServiceUri = ServiceUri);

		using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IHttpClientFactory>();

		// Named after the concrete client, so this registration and a concrete
		// AddRemoteClient<OrdersApiClient> address the same named HttpClient.
		factory.CreateClient(nameof(OrdersApiClient)).BaseAddress.Should().Be(ServiceUri);
		// The interface name is not the client name — nothing is configured under it.
		factory.CreateClient(nameof(IOrdersApi)).BaseAddress.Should().BeNull();
	}

	[Fact]
	public void An_explicit_client_name_still_wins() {
		var services = new ServiceCollection();
		var builder = BuilderWith(services);

		builder.AddRemoteClient<IOrdersApi, OrdersApiClient>(
			o => o.ServiceUri = ServiceUri,
			clientName: "orders");

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IHttpClientFactory>()
			.CreateClient("orders").BaseAddress.Should().Be(ServiceUri);
	}

	[Fact]
	public void The_application_name_reaches_the_configured_client() {
		var services = new ServiceCollection();
		var builder = BuilderWith(services);

		builder.AddRemoteClient<IOrdersApi, OrdersApiClient>(
			o => o.ServiceUri = ServiceUri,
			applicationName: "Portal");

		using var provider = services.BuildServiceProvider();
		var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OrdersApiClient));
		client.DefaultRequestHeaders.GetValues(RemoteIdentityConstants.AppNameHeader)
			.Should().ContainSingle().Which.Should().Be("Portal");
	}

	[Fact]
	public void The_options_overload_registers_the_same_way() {
		var services = new ServiceCollection();
		var builder = BuilderWith(services);

		builder.AddRemoteClient<IOrdersApi, OrdersApiClient>(new RemoteServiceOptions { ServiceUri = ServiceUri });

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IOrdersApi>().Should().BeOfType<OrdersApiClient>();
		provider.GetRequiredService<IHttpClientFactory>()
			.CreateClient(nameof(OrdersApiClient)).BaseAddress.Should().Be(ServiceUri);
	}

	[Fact]
	public void The_implementation_receives_the_configured_HttpClient() {
		var services = new ServiceCollection();
		var builder = BuilderWith(services);

		builder.AddRemoteClient<IOrdersApi, OrdersApiClient>(o => o.ServiceUri = ServiceUri);

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IOrdersApi>().Client.BaseAddress.Should().Be(ServiceUri);
	}

}
