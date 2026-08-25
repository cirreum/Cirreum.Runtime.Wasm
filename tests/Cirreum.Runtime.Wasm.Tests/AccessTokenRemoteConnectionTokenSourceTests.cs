namespace Cirreum.Runtime.Wasm.Tests;

using Cirreum.RemoteServices;
using Cirreum.Runtime.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

public class AccessTokenRemoteConnectionTokenSourceTests {

	// ---------------------------------------------------------------------
	// Harness
	// ---------------------------------------------------------------------

	// AccessTokenResult's constructor is the only way to shape a result: the status and the
	// token travel together, and TryGetToken reads both.
	private sealed class StubTokenProvider(AccessTokenResult result) : IAccessTokenProvider {

		public ValueTask<AccessTokenResult> RequestAccessToken() => new(result);

		public ValueTask<AccessTokenResult> RequestAccessToken(AccessTokenRequestOptions options) => new(result);

	}

	private static IRemoteConnectionTokenSource CreateSource(AccessTokenResult result) {
		return new AccessTokenRemoteConnectionTokenSource(new StubTokenProvider(result));
	}

	private static AccessTokenResult Success(string value) {
		return new AccessTokenResult(
			AccessTokenResultStatus.Success,
			new AccessToken { Value = value, Expires = DateTimeOffset.UtcNow.AddMinutes(5), GrantedScopes = [] },
			interactiveRequestUrl: null,
			interactiveRequest: null);
	}

	private static AccessTokenResult RequiresRedirect() {
		return new AccessTokenResult(
			AccessTokenResultStatus.RequiresRedirect,
			token: null!,
			interactiveRequestUrl: "https://localhost/authentication/login",
			interactiveRequest: new InteractiveRequestOptions {
				Interaction = InteractionType.GetToken,
				ReturnUrl = "https://localhost/",
			});
	}

	// ---------------------------------------------------------------------
	// Tests
	// ---------------------------------------------------------------------

	[Fact]
	public async Task GetAccessTokenAsync_returns_the_token_when_one_is_available() {

		var source = CreateSource(Success("token-value"));

		var token = await source.GetAccessTokenAsync();

		token.Should().Be("token-value");

	}

	[Fact]
	public async Task GetAccessTokenAsync_returns_null_when_the_token_requires_interaction() {

		// The connection is mid-handshake: a redirect raised here would navigate the
		// application away from whatever the user was doing.
		var source = CreateSource(RequiresRedirect());

		var token = await source.GetAccessTokenAsync();

		token.Should().BeNull();

	}

}
