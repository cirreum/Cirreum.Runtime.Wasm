namespace Cirreum.Runtime.Authentication.PostProcessors;

using Cirreum.Runtime.Security;
using Cirreum.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

/// <summary>
/// Runs registered <see cref="IUserProfileEnricher"/> implementations after authentication
/// to populate extended profile properties on the authenticated user.
/// </summary>
/// <remarks>
/// <para>
/// Runs as part of Phase 2 (deferred, best-effort) in
/// <see cref="CommonClaimsPrincipalFactory{TAccount}"/> after the
/// <see cref="ClaimsPrincipal"/> has been fully constructed and returned to Blazor.
/// </para>
/// <para>
/// Enrichment completion is always marked via <see cref="ClientUser.SetEnrichmentCompleted"/>
/// regardless of whether an enricher is registered or whether enrichment succeeds,
/// so consumers can rely on <see cref="UserProfile.IsEnriched"/> as a stable signal.
/// </para>
/// </remarks>
sealed partial class ProfileEnrichmentProcessor(
	ILogger<ProfileEnrichmentProcessor> logger
) : IAuthenticationPostProcessor {

	public int Order { get; } = 200;

	/// <inheritdoc/>
	public async Task ProcessAsync(
		IServiceProvider serviceProvider,
		IUserState userState,
		CancellationToken cancellationToken = default) {

		if (userState is not ClientUser clientUser) {
			Log.InvalidUserStateType(logger, userState?.GetType().Name ?? "null");
			return;
		}

		var enricher = serviceProvider.GetService<IUserProfileEnricher>();
		if (enricher is null) {
			// No enricher registered — mark complete and move on
			clientUser.SetEnrichmentCompleted();
			return;
		}

		try {
			await enricher.EnrichProfileAsync(clientUser.Profile, clientUser.Identity!);
		} catch (Exception ex) {
			Log.EnrichmentFailed(logger, ex);
		} finally {
			// Always mark enrichment complete so consumers can rely on IsEnriched
			// as a stable signal regardless of whether enrichment succeeded.
			clientUser.SetEnrichmentCompleted();
		}
	}

	// -------------------------------------------------------------------------
	// Logging
	// -------------------------------------------------------------------------

	private static partial class Log {
		[LoggerMessage(Level = LogLevel.Error, Message = "Invalid user state instance type '{ActualType}', expected 'ClientUser'.")]
		internal static partial void InvalidUserStateType(ILogger logger, string actualType);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Profile enrichment failed. Enrichment marked complete with partial data.")]
		internal static partial void EnrichmentFailed(ILogger logger, Exception ex);
	}

}