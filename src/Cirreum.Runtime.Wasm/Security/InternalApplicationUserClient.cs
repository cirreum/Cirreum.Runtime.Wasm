namespace Cirreum.Runtime.Security;

using Cirreum.RemoteServices;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;

/// <summary>
/// The framework's application-user bootstrap client, closed over the app's own user type by
/// <c>AddApplicationUser&lt;TUser&gt;</c>. Calls <see cref="ApplicationUserEndpoint.Route"/> —
/// which requires authentication and nothing else — so the record arrives even for a disabled
/// caller, letting the router render the disabled experience instead of falling through to
/// "not provisioned".
/// </summary>
/// <typeparam name="TUser">
/// The app's application-user implementation. The server serializes its resolver's runtime
/// type, so this must be the type (or a compatible shape of the type) the server-side
/// <see cref="IApplicationUserResolver"/> returns.
/// </typeparam>
internal sealed class InternalApplicationUserClient<TUser>(
	HttpClient client,
	ILogger<InternalApplicationUserClient<TUser>> logger,
	IDomainEnvironment domainEnvironment)
	: ApplicationUserClient(client, logger, domainEnvironment)
	where TUser : class, IApplicationUser {

	/// <inheritdoc/>
	public override async Task<IApplicationUser?> ResolveAsync(CancellationToken cancellationToken = default) {

		// GetRawAsync rather than GetAsync<TUser>: the endpoint's "no record" answer is a
		// bodyless 204, which the JSON pipeline would misread as a failure.
		using var response = await this.GetRawAsync(ApplicationUserEndpoint.Route, cancellationToken);

		if (response.StatusCode == HttpStatusCode.NoContent) {
			return null;
		}

		return await response.Content.ReadFromJsonAsync<TUser>(this.JsonOptions, cancellationToken);

	}

}
