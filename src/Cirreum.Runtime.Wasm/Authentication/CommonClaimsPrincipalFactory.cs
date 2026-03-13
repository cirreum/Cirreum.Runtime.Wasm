namespace Cirreum.Runtime.Authentication;

using Cirreum.Runtime.Security;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

/// <summary>
/// Parses the newly created claims principal and synchronizes <see cref="IUserState"/>.
/// </summary>
/// <remarks>
/// <para>
/// Handles identity mapping, claims extension, and <see cref="ClientUser"/> principal
/// assignment inline within Blazor's <c>CreateUserAsync</c> pipeline. Returns immediately
/// after setting the authenticated principal so the login-callback flow is unblocked.
/// </para>
/// <para>
/// Application user loading (<see cref="IApplicationUserFactory"/>), profile enrichment
/// (<see cref="IUserProfileEnricher"/>), and remote state initialization are handled
/// separately by the <see cref="IInitializationOrchestrator"/> once <c>AppRouteView</c>
/// triggers initialization after authentication settles.
/// </para>
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
	private long _lastProcessedTimestamp;
	private static readonly TimeSpan deduplicationWindow = TimeSpan.FromSeconds(30);

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
			this.OnAuthenticatedUser(userPrincipal, identity, account);
			return ValueTask.FromResult(userPrincipal);
		}

		return ValueTask.FromResult((ClaimsPrincipal)this.SetAnonymous());

	}

	// -------------------------------------------------------------------------
	// Anonymous Path
	// -------------------------------------------------------------------------

	private AnonymousUser SetAnonymous() {
		this._lastProcessedTimestamp = 0;
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

	private void OnAuthenticatedUser(
		ClaimsPrincipal userPrincipal,
		ClaimsIdentity identity,
		TAccount account) {

		if (!this.ShouldProcessPrincipal(userPrincipal)) {
#if DEBUG
			Console.WriteLine($"warn: CommonClaimsPrincipalFactory => SKIPPING CreateUserAsync() - ACCOUNT {account} @ {DateTime.Now} - same user within deduplication window of {deduplicationWindow.TotalSeconds}s");
#endif
			return;
		}

		try {
			this.MapIdentity(identity, account);
			this.ExtendClaims(identity, account);
			this.UpdatePrincipalTracking(userPrincipal);
		} catch (Exception e) {
			logger.LogCreateUserError(e);
		}

		// Set the authenticated principal on ClientUser and notify state subscribers.
		// App user loading, profile enrichment, and remote state initialization are
		// handled by IInitializationOrchestrator after AppRouteView triggers init.
		var clientUser = serviceProvider.GetRequiredService<ClientUser>();
		clientUser.SetAuthenticatedPrincipal(userPrincipal);

		var stateManager = serviceProvider.GetService<IStateManager>();
		stateManager?.NotifySubscribers<IUserState>(clientUser);
		logger.LogUserStateChanged(clientUser.Name ?? "unknown");

	}

	// -------------------------------------------------------------------------
	// Tracking
	// -------------------------------------------------------------------------

	private bool ShouldProcessPrincipal(ClaimsPrincipal principal) {
		var id = ClaimsHelper.ResolveId(principal) ?? "";
		var name = ClaimsHelper.ResolveName(principal) ?? "";
		return id != this._lastProcessedId ||
			   name != this._lastProcessedName ||
			   Stopwatch.GetElapsedTime(this._lastProcessedTimestamp) >= deduplicationWindow;
	}

	private void UpdatePrincipalTracking(ClaimsPrincipal principal) {
		this._lastProcessedId = ClaimsHelper.ResolveId(principal) ?? "";
		this._lastProcessedName = ClaimsHelper.ResolveName(principal) ?? "";
		this._lastProcessedTimestamp = Stopwatch.GetTimestamp();
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

}