namespace Cirreum.Runtime.Wasm.Tests;

using Cirreum.Runtime;
using Cirreum.Runtime.Security;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Guards the registration seam <c>AppRouteView</c> and <c>InitializationOrchestrator</c>
/// depend on: <c>AddApplicationUser&lt;TUser&gt;</c> must make the framework's
/// <see cref="ApplicationUserClient"/> anchor discoverable — via
/// <see cref="IServiceProviderIsService"/> (the view's presence probe) and via resolution
/// (the orchestrator's lookup). The 2.0.0 regression this locks: the view probed for the
/// removed <c>IApplicationUserResolver</c>, so the NotProvisioned/Disabled states were
/// unreachable for every app on the new registration.
/// </summary>
public sealed class AddApplicationUserRegistrationTests {

	private sealed class TestUser : IApplicationUser {
		public bool IsEnabled => true;
		public IReadOnlyList<string> Roles => [];
	}

	private sealed class StubDomainEnvironment : IDomainEnvironment {
		public string ApplicationName => "tests";
		public string EnvironmentName => "Development";
		public DomainRuntimeType RuntimeType => default;
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

	private static readonly Uri ServiceUri = new("https://api.example.com/");

	[Fact]
	public void AddApplicationUser_makes_the_client_anchor_discoverable_without_construction() {
		var services = new ServiceCollection();

		services.AddApplicationUser<TestUser>(ServiceUri);

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IServiceProviderIsService>()
			.IsService(typeof(ApplicationUserClient)).Should().BeTrue();
	}

	[Fact]
	public void Without_AddApplicationUser_the_anchor_is_absent() {
		var services = new ServiceCollection();

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IServiceProviderIsService>()
			.IsService(typeof(ApplicationUserClient)).Should().BeFalse();
	}

	[Fact]
	public void The_anchor_resolves_to_the_typed_client_closed_over_the_users_type() {
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IDomainEnvironment>(new StubDomainEnvironment());
		// The default remote-service branch wires AuthorizationMessageHandler into the
		// named client's pipeline; a real app gets it from AddOidcAuthentication /
		// AddMsalAuthentication.
		services.AddScoped(_ =>
			new Microsoft.AspNetCore.Components.WebAssembly.Authentication.AuthorizationMessageHandler(
				new StubAccessTokenProvider(), new StubNavigationManager()));

		services.AddApplicationUser<TestUser>(ServiceUri);

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<ApplicationUserClient>()
			.Should().BeOfType<InternalApplicationUserClient<TestUser>>();
	}

	[Fact]
	public void A_second_registration_throws_per_the_single_idp_client_invariant() {
		var services = new ServiceCollection();
		services.AddApplicationUser<TestUser>(ServiceUri);

		var act = () => services.AddApplicationUser<TestUser>(ServiceUri);

		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*already been called*");
	}

}
