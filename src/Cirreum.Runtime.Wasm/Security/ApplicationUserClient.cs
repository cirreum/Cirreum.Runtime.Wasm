namespace Cirreum.Runtime.Security;

using Cirreum.RemoteServices;
using Microsoft.Extensions.Logging;

/// <summary>
/// The non-generic anchor for the framework's application-user bootstrap client.
/// <see cref="InternalApplicationUserClient{TUser}"/> closes it over the app's user type;
/// <c>InitializationOrchestrator</c> resolves this base — a concrete framework type, never
/// <see cref="IApplicationUserResolver"/> — so a hand-registered client implementation cannot
/// silently take over the bootstrap path.
/// </summary>
internal abstract class ApplicationUserClient(
	HttpClient client,
	ILogger logger,
	IDomainEnvironment domainEnvironment
) : RemoteClient(client, logger, domainEnvironment, activitySource: null) {

	/// <summary>
	/// The named <see cref="HttpClient"/> registration backing this client.
	/// </summary>
	internal const string ClientName = "CirreumApplicationUserClient";

	/// <summary>
	/// Resolves the caller's own application user from the framework bootstrap endpoint
	/// (<see cref="ApplicationUserEndpoint.Route"/>).
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The caller's application user, or <see langword="null"/> when the caller has no
	/// record (the endpoint's 204).
	/// </returns>
	public abstract Task<IApplicationUser?> ResolveAsync(CancellationToken cancellationToken = default);

}
