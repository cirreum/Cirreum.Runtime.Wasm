namespace Cirreum.Runtime.Authentication;

using Cirreum.Runtime.Security;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;

/// <summary>
/// Parses the newly created claims principal and synchronizes <see cref="IUserState"/>.
/// </summary>
/// <remarks>
/// <para>
/// Authentication is split into two phases to minimize latency in Blazor's
/// <c>CreateUserAsync</c> pipeline:
/// </para>
/// <list type="bullet">
///   <item>
///     <strong>Phase 1 — Inline, blocking, critical:</strong> Identity mapping and claims
///     extension. Must complete before <c>CreateUserAsync</c> returns so that Blazor
///     receives a fully enriched <see cref="ClaimsPrincipal"/>.
///   </item>
///   <item>
///     <strong>Phase 2 — Deferred, non-blocking, best effort:</strong> App user hydration
///     via post-processors and async state notification. Deferred via <c>Task.Yield</c>
///     so <c>CreateUserAsync</c> returns immediately after Phase 1. Stays on the Blazor
///     synchronization context — no <c>Task.Run</c>.
///   </item>
/// </list>
/// </remarks>
public abstract partial class CommonClaimsPrincipalFactory<TAccount>(
	ILogger logger,
	IServiceProvider serviceProvider,
	IAccessTokenProviderAccessor tokenAccessor,
	IEnumerable<IClaimsExtender>? claimsExtenders = null
) : AccountClaimsPrincipalFactory<TAccount>(tokenAccessor)
	where TAccount : RemoteUserAccount {

	private string? _lastProcessedId;
	private string? _lastProcessedName;
	private DateTimeOffset _lastProcessedTime;
	private static readonly TimeSpan CooldownPeriod = TimeSpan.FromSeconds(30);

	// -------------------------------------------------------------------------
	// CreateUserAsync Entry Point
	// -------------------------------------------------------------------------

	/// <inheritdoc/>
	public override ValueTask<ClaimsPrincipal> CreateUserAsync(
		TAccount account,
		RemoteAuthenticationUserOptions options) {

		if (account is null) {
#if DEBUG
			Console.WriteLine($"CommonClaimsPrincipalFactory => CreateUserAsync() - ACCOUNT IS NULL @ {DateTime.Now}");
#endif
			logger.LogCreateUser(AnonymousUser.AnonymousUserName);
			return ValueTask.FromResult((ClaimsPrincipal)this.SetAnonymous());
		}

#if DEBUG
		Console.WriteLine($"CommonClaimsPrincipalFactory => CreateUserAsync() - ACCOUNT {account} @ {DateTime.Now}");
#endif

		var userPrincipal = CreatePrincipal(account, options);
		var userPrincipalName = userPrincipal.Identity?.Name ?? "unknown";
		logger.LogCreateUser(userPrincipalName);

		if (userPrincipal.Identity is ClaimsIdentity identity && identity.IsAuthenticated) {
			return this.OnAuthenticatedUser(userPrincipal, identity, account);
		}

		return ValueTask.FromResult((ClaimsPrincipal)this.SetAnonymous());

	}

	// -------------------------------------------------------------------------
	// Anonymous Path
	// -------------------------------------------------------------------------

	private AnonymousUser SetAnonymous() {
		this._lastProcessedTime = DateTimeOffset.MinValue;
		this._lastProcessedId = null;
		this._lastProcessedName = null;

		var clientUser = serviceProvider.GetService<ClientUser>();
		if (clientUser?.IsAuthenticated == true) {
			clientUser.SetAnonymous();
			// Anonymous transition is sync — lightweight, no app user concern
			var stateManager = serviceProvider.GetService<IStateManager>();
			stateManager?.NotifySubscribers<IUserState>(clientUser);
		} else {
			clientUser?.SetAnonymous();
		}

		return AnonymousUser.Shared;
	}

	// -------------------------------------------------------------------------
	// Authenticated Path
	// -------------------------------------------------------------------------

	private async ValueTask<ClaimsPrincipal> OnAuthenticatedUser(
		ClaimsPrincipal userPrincipal,
		ClaimsIdentity identity,
		TAccount account) {

		if (!this.ShouldProcessPrincipal(userPrincipal)) {
#if DEBUG
			Console.WriteLine($"warn: CommonClaimsPrincipalFactory => SKIPPING CreateUserAsync() - ACCOUNT {account} @ {DateTime.Now} - same user within cooldown of {CooldownPeriod.TotalSeconds}s");
#endif
			return userPrincipal;
		}

		try {
			this.MapIdentity(identity, account);
			this.ExtendClaims(identity, account);
			this.UpdatePrincipalTracking(userPrincipal);
		} catch (Exception e) {
			logger.LogCreateUserError(e);
			return userPrincipal;
		}

		// Optionally create application user from application database
		var clientUser = await this.CreateApplicationUser(userPrincipal);

		// Deferred processing for Idp UserProfile enrichment and state notification
		// Don't block - return to login-callback flow immediately
		_ = Task.Run(() => this.RunDeferredUserProcessingAsync(clientUser));

		return userPrincipal;

	}

	private async Task RunDeferredUserProcessingAsync(ClientUser clientUser) {
		var userName = clientUser.Name ?? "unknown";
		try {
			await this.RunUserProfileEnricher(clientUser);
			var stateManager = serviceProvider.GetService<IStateManager>();
			stateManager?.NotifySubscribers<IUserState>(clientUser);
			logger.LogUserStateChanged(userName);
		} catch (Exception e) {
			logger.LogUserStateProcessingError(e, userName);
		}
	}

	// -------------------------------------------------------------------------
	// Tracking
	// -------------------------------------------------------------------------

	private bool ShouldProcessPrincipal(ClaimsPrincipal principal) {
		var id = ClaimsHelper.ResolveId(principal) ?? "";
		var name = ClaimsHelper.ResolveName(principal) ?? "";
		var now = DateTimeOffset.Now;
		return id != this._lastProcessedId ||
			   name != this._lastProcessedName ||
			   now >= this._lastProcessedTime + CooldownPeriod;
	}

	private void UpdatePrincipalTracking(ClaimsPrincipal principal) {
		this._lastProcessedId = ClaimsHelper.ResolveId(principal) ?? "";
		this._lastProcessedName = ClaimsHelper.ResolveName(principal) ?? "";
		this._lastProcessedTime = DateTimeOffset.Now;
	}

	private static ClaimsPrincipal CreatePrincipal(
		TAccount account,
		RemoteAuthenticationUserOptions options) {

		var identity = account != null ? new ClaimsIdentity(
		   options.AuthenticationType,
		   options.NameClaim,
		   options.RoleClaim) : new ClaimsIdentity();

		if (account != null) {
			foreach (var kvp in account.AdditionalProperties) {
				var name = kvp.Key;
				var value = kvp.Value;
				if (value != null ||
					(value is JsonElement element && element.ValueKind != JsonValueKind.Undefined && element.ValueKind != JsonValueKind.Null)) {
					identity.AddClaim(new Claim(name, value.ToString()!));
				}
			}
		}

		return new ClaimsPrincipal(identity);

	}

	// -------------------------------------------------------------------------
	// Extension Points
	// -------------------------------------------------------------------------

	/// <summary>
	/// Override to provide custom claims mapping for the <see cref="ClaimsIdentity"/>.
	/// Called in Phase 1 — must complete before <c>CreateUserAsync</c> returns.
	/// </summary>
	protected virtual void MapIdentity(ClaimsIdentity identity, TAccount account) { }

	/// <summary>
	/// Default implementation for calling each registered <see cref="IClaimsExtender"/>.
	/// Called in Phase 1 — must complete before <c>CreateUserAsync</c> returns.
	/// </summary>
	protected virtual void ExtendClaims(ClaimsIdentity identity, TAccount account) {
		if (claimsExtenders is not null) {
			foreach (var extender in claimsExtenders.OrderBy(e => e.Order)) {
				try {
					extender.ExtendClaims(identity, account);
				} catch (Exception ex) {
					var extenderName = extender.GetType().FullName ?? "Unknown Extender";
					logger.LogClaimsExtenderError(ex, extenderName);
				}
			}
		}
	}

	/// <summary>
	/// Creates and configures a new application user based on the provided claims principal.
	/// </summary>
	/// <remarks>If an application user cannot be loaded, the returned ClientUser will indicate that loading was
	/// attempted but no user was found. The method does not throw if user loading fails; instead, it sets the
	/// ApplicationUser property to null and marks the user as loaded.</remarks>
	/// <param name="claimsPrincipal">The claims principal containing authentication and identity information used to initialize the application user.</param>
	/// <returns>A configured ClientUser instance representing the authenticated user. If no application user can be loaded, the
	/// ApplicationUser property will be null.</returns>
	private async Task<ClientUser> CreateApplicationUser(ClaimsPrincipal claimsPrincipal) {
		var clientUser = serviceProvider.GetRequiredService<ClientUser>();
		clientUser.SetAuthenticatedPrincipal(claimsPrincipal);
		var appUserFactory = serviceProvider.GetServices<IApplicationUserFactory>();
		if (appUserFactory is null) {
			return clientUser;
		}
		//var result = appUserFactory.CreateUserAsync(clientUser.Id)
		/*
		 if (result.IsSuccess) {
			clientUser.SetAppUser(result.Value);
			logger.UserLoaded(result.Value.GetType().Name);
			return clientUser;
		}
			 */

		// Set to null to indicate that loading was attempted but no user was found/loadable.
		// This sets IsApplicationUserLoaded = true with ApplicationUser = null.
		// Using ClearApplicationUser() would reset IsApplicationUserLoaded = false, indicating
		// no load attempt was made.
		clientUser.SetAppUser(null);
		//logger.UserLoadFailed(result.Error, typeof(T).Name);

		return clientUser;
	}

	/// <summary>
	/// Attempts to enrich the user profile for the specified client user using a registered profile enricher service.
	/// </summary>
	/// <remarks>If no profile enricher service is registered, the enrichment is marked as completed without
	/// performing any enrichment. The method ensures that the enrichment completion state is always set, allowing
	/// consumers to reliably check whether enrichment has finished. Any exceptions during enrichment are logged as
	/// warnings.</remarks>
	/// <param name="clientUser">The client user whose profile will be enriched. Cannot be null. The enrichment process updates the user's profile
	/// and marks enrichment as completed.</param>
	/// <returns>A task that represents the asynchronous operation. The task completes when the enrichment process has finished,
	/// regardless of success or failure.</returns>
	private async Task RunUserProfileEnricher(ClientUser clientUser) {

		var enricher = serviceProvider.GetService<IUserProfileEnricher>();
		if (enricher is null) {
			// No enricher registered — mark complete and move on
			clientUser.SetEnrichmentCompleted();
			return;
		}

		try {
			await enricher.EnrichProfileAsync(clientUser.Profile, clientUser.Identity!);
		} catch (Exception ex) {
			logger.LogWarning(ex, "UserProfile enrichment failed");
		} finally {
			// Always mark enrichment complete so consumers can rely on IsEnriched
			// as a stable signal regardless of whether enrichment succeeded.
			clientUser.SetEnrichmentCompleted();
		}

	}

}