namespace Cirreum.Runtime.Authentication;

using Cirreum.RemoteServices;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

/// <summary>
/// Supplies the browser session's access token to outbound remote connections.
/// </summary>
/// <remarks>
/// Returns <see langword="null"/> when the token provider cannot satisfy the request without
/// user interaction. A connection resolving a credential is mid-handshake, often on a
/// background reconnect, and a redirect raised from there would navigate the application away
/// from whatever the user was doing.
/// </remarks>
internal sealed class AccessTokenRemoteConnectionTokenSource(IAccessTokenProvider tokenProvider)
	: IRemoteConnectionTokenSource {

	public async ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) {

		var result = await tokenProvider.RequestAccessToken();
		return result.TryGetToken(out var token) ? token.Value : null;

	}

}
