namespace Cirreum.Runtime.Wasm.Tests;

using Cirreum.RemoteServices;
using Cirreum.RemoteServices.Connections;
using Cirreum.Runtime.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Logging.Abstractions;

public class SessionRemoteConnectionCredentialSourceTests {

	// ---------------------------------------------------------------------
	// Harness
	// ---------------------------------------------------------------------

	private sealed class ChatConnection;

	// AccessTokenResult's constructor is the only way to shape a result: the status and the
	// token travel together, and TryGetToken reads both.
	private sealed class StubTokenProvider(AccessTokenResult result) : IAccessTokenProvider {

		public AccessTokenRequestOptions? LastOptions { get; private set; }

		public int Calls { get; private set; }

		public ValueTask<AccessTokenResult> RequestAccessToken() {
			this.Calls++;
			return new(result);
		}

		public ValueTask<AccessTokenResult> RequestAccessToken(AccessTokenRequestOptions options) {
			this.Calls++;
			this.LastOptions = options;
			return new(result);
		}

	}

	private static AccessTokenResult Success(string value) =>
		new(AccessTokenResultStatus.Success,
			new AccessToken { Value = value, Expires = DateTimeOffset.UtcNow.AddMinutes(5), GrantedScopes = [] },
			interactiveRequestUrl: null,
			interactiveRequest: null);

	private static AccessTokenResult RequiresRedirect() =>
		new(AccessTokenResultStatus.RequiresRedirect,
			token: null!,
			interactiveRequestUrl: "https://localhost/authentication/login",
			interactiveRequest: new InteractiveRequestOptions {
				Interaction = InteractionType.GetToken,
				ReturnUrl = "https://localhost/",
			});

	private static RemoteConnectionCredentialRequest Request(params string[] scopes) =>
		new() {
			EndpointUri = new Uri("https://api.contoso.com/hubs/chat"),
			Scopes = scopes,
			ConnectionType = typeof(ChatConnection),
		};

	private static (IRemoteConnectionCredentialSource Source, StubTokenProvider Provider) Create(
		AccessTokenResult result) {

		var provider = new StubTokenProvider(result);
		return (new SessionRemoteConnectionCredentialSource(
			provider, NullLogger<SessionRemoteConnectionCredentialSource>.Instance), provider);

	}

	// ---------------------------------------------------------------------
	// The token is requested for the audience the connection named
	// ---------------------------------------------------------------------

	[Fact]
	public async Task TheTokenIsRequestedForTheDeclaredScopes() {

		var (source, provider) = Create(Success("token-value"));

		var credential = await source.GetCredentialAsync(Request("api://contoso/access_as_user"));

		provider.LastOptions.Should().NotBeNull("the scopes must reach the token provider");
		provider.LastOptions!.Scopes.Should().Equal("api://contoso/access_as_user");
		credential.Should().NotBeNull();
		credential!.Scheme.Should().Be("Bearer");
		credential.Value.Should().Be("token-value");

	}

	[Fact]
	public async Task AllDeclaredScopes_AreRequestedTogether() {

		var (_, provider) = Create(Success("token-value"));
		var source = new SessionRemoteConnectionCredentialSource(
			provider, NullLogger<SessionRemoteConnectionCredentialSource>.Instance);

		await source.GetCredentialAsync(Request("api://contoso/read", "api://contoso/write"));

		provider.LastOptions!.Scopes.Should().Equal("api://contoso/read", "api://contoso/write");

	}

	// ---------------------------------------------------------------------
	// No scopes means no credential, not a default-scope token
	// ---------------------------------------------------------------------

	[Fact]
	public async Task DeclaringNoScopes_SuppliesNoCredential() {

		// The host's default scopes address whatever resource the application signed in against -
		// on Entra, Microsoft Graph. A token for those is rejected by a first-party API, and
		// rejected at the server, which reads as an application authentication fault.
		var (source, provider) = Create(Success("graph-token"));

		var credential = await source.GetCredentialAsync(Request());

		credential.Should().BeNull();
		provider.Calls.Should().Be(0, "the token provider must not be asked for the host's defaults");

	}

	// ---------------------------------------------------------------------
	// Interaction is never raised from a handshake
	// ---------------------------------------------------------------------

	[Fact]
	public async Task WhenATokenNeedsInteraction_NoCredentialIsSuppliedAndNoRedirectIsRaised() {

		// A connection resolving a credential is mid-handshake, often on a background reconnect.
		var (source, _) = Create(RequiresRedirect());

		var credential = await source.GetCredentialAsync(Request("api://contoso/access_as_user"));

		credential.Should().BeNull();

	}

}
