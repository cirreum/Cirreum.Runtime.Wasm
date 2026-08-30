namespace Cirreum.Runtime.Authentication;

using Cirreum.RemoteServices;
using Cirreum.RemoteServices.Connections;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Logging;

/// <summary>
/// Supplies the browser session's access token to outbound remote connections.
/// </summary>
/// <remarks>
/// <para>
/// The token is requested for the scopes the connection declares. A connection that declares
/// none is not given a token: the host's default scopes address whatever resource the
/// application signed in against, which is rarely the endpoint being connected to.
/// </para>
/// <para>
/// Returns no credential when one cannot be obtained without user interaction. A connection
/// resolving a credential is mid-handshake, often on a background reconnect, and a redirect
/// raised from there would navigate the application away from whatever the user was doing.
/// </para>
/// </remarks>
internal sealed partial class SessionRemoteConnectionCredentialSource(
	IAccessTokenProvider tokenProvider,
	ILogger<SessionRemoteConnectionCredentialSource> logger)
	: IRemoteConnectionCredentialSource {

	private const string BearerScheme = "Bearer";

	public async ValueTask<AuthorizationHeaderSettings?> GetCredentialAsync(
		RemoteConnectionCredentialRequest request,
		CancellationToken cancellationToken = default) {

		ArgumentNullException.ThrowIfNull(request);

		if (request.Scopes.Count == 0) {
			logger.LogNoScopesDeclared(request.ConnectionType.Name, request.EndpointUri.ToString());
			return null;
		}

		var result = await tokenProvider.RequestAccessToken(new AccessTokenRequestOptions {
			Scopes = request.Scopes,
		});

		if (!result.TryGetToken(out var token)) {
			logger.LogTokenUnavailable(request.ConnectionType.Name, result.Status.ToString());
			return null;
		}

		return new AuthorizationHeaderSettings { Scheme = BearerScheme, Value = token.Value };

	}

}
