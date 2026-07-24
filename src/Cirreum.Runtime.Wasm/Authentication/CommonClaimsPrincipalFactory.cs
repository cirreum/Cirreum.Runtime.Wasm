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
/// Application user loading (<see cref="IApplicationUserResolver"/>), profile enrichment
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

	private string? _lastPublishedId;
	private int _lastPublishedFingerprint;
	private long _lastPublishedTimestamp;
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
		this._lastPublishedTimestamp = 0;
		this._lastPublishedId = null;
		this._lastPublishedFingerprint = 0;

		var clientUser = serviceProvider.GetRequiredService<ClientUser>();

		var wasAuthComplete = clientUser.IsAuthenticationComplete;
		var wasAuthenticated = clientUser.IsAuthenticated;
		clientUser.SetAnonymous();

		if (!wasAuthComplete || wasAuthenticated) {
			// First time auth settled — notify subscribers
			// Specifically, AppRouteView, so it can either Redirect
			// to the login page or trigger initialization for anonymous users.
			var stateManager = serviceProvider.GetService<IStateManager>();
			stateManager?.NotifySubscribers<IUserState>(clientUser);
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

		// Always run the claim transforms against the newly created principal. The returned
		// principal becomes Blazor's authentication state and must include all available
		// mapping, canonicalization, and extender output even when an identical state
		// publication is deduplicated below. Each transform is isolated so one failing
		// concern does not suppress the others.
		try {
			this.MapIdentity(identity, account);
		} catch (Exception e) {
			logger.LogCreateUserError(e);
		}

		try {
			// Alias provisioned custom* claims to their native names (customRoles -> roles, …)
			// before app extenders run, so IClaimsExtender, IsInRole, and UserProfile read native
			// claims. Additive only — precedence between a native and a custom claim is left to the
			// app's own extender (see CustomClaimCanonicalizer).
			CustomClaimCanonicalizer.Canonicalize(identity);
		} catch (Exception e) {
			logger.LogCreateUserError(e);
		}

		try {
			// The base implementation isolates each extender individually; this guard covers a
			// derived override that throws outside that isolation.
			this.ExtendClaims(identity, account);
		} catch (Exception e) {
			logger.LogCreateUserError(e);
		}

		// Publication dedupes on (user id, claims fingerprint) inside the window: an identical
		// duplicate call (the login double-fire) skips re-publication entirely, while ANY
		// content change — refreshed roles, different extender output — publishes in full.
		if (!this.ShouldPublishPrincipal(identity, out var id, out var fingerprint)) {
#if DEBUG
			Console.WriteLine($"warn: CommonClaimsPrincipalFactory => SKIPPING state re-publication - ACCOUNT {account} @ {DateTime.Now} - same user and claims within deduplication window of {deduplicationWindow.TotalSeconds}s");
#endif
			return;
		}

		// ClientUser mutation and subscriber notification form one logical publication and are
		// always performed together by this factory — rebuilding UserProfile must be paired
		// with notification so initialization can restore enrichment. The state manager is
		// optional by composition: when none is registered there are no state subscribers to
		// notify, and the mutation alone is the whole publication.
		// App user loading, profile enrichment, and remote state initialization are handled
		// by IInitializationOrchestrator after AppRouteView triggers init.
		var clientUser = serviceProvider.GetRequiredService<ClientUser>();
		clientUser.SetAuthenticatedPrincipal(userPrincipal);

		var stateManager = serviceProvider.GetService<IStateManager>();
		stateManager?.NotifySubscribers<IUserState>(clientUser);
		logger.LogUserStateChanged(clientUser.Name ?? "unknown");

		// Record the exact principal content that was actually published — including a
		// partially transformed one. Transforms are re-attempted on every invocation anyway;
		// if a failed transform later succeeds, the changed fingerprint causes another
		// publication automatically, while a persistently failing one dedupes like any other
		// identical content instead of re-publishing on every duplicate call. An interrupted
		// publication (a throw above) is never recorded, so the next call retries.
		this.UpdatePrincipalTracking(id, fingerprint);

	}

	// -------------------------------------------------------------------------
	// Tracking
	// -------------------------------------------------------------------------

	private bool ShouldPublishPrincipal(ClaimsIdentity identity, out string id, out int fingerprint) {
		id = ClaimsHelper.ResolveId(identity) ?? "";
		fingerprint = ComputeClaimsFingerprint(identity);
		return id != this._lastPublishedId ||
			   fingerprint != this._lastPublishedFingerprint ||
			   Stopwatch.GetElapsedTime(this._lastPublishedTimestamp) >= deduplicationWindow;
	}

	private void UpdatePrincipalTracking(string id, int fingerprint) {
		this._lastPublishedId = id;
		this._lastPublishedFingerprint = fingerprint;
		this._lastPublishedTimestamp = Stopwatch.GetTimestamp();
	}

	// Content hash over the processed identity. Claims are sorted before hashing, so the
	// fingerprint is order-insensitive — app-defined transforms may emit the same effective
	// set in a different order. Effective content is deliberately (type, value) plus the
	// identity's authentication/name/role claim-type configuration (the same claims behave
	// differently under a different NameClaimType/RoleClaimType); provenance metadata
	// (ValueType, Issuer, Properties) is excluded, matching the canonicalizer's own equality.
	// Any content change — a refreshed role, different extender output — changes the value, so
	// a same-user call with new claims publishes instead of being deduplicated; a volatile
	// per-call claim from an app extender disengages deduplication for that user, which is the
	// correct outcome since content genuinely differs. The 32-bit width is an accepted trade
	// for this in-memory, window-bounded suppression: a collision could only skip one
	// republication of changed content for at most the window, after which time republishes.
	private static int ComputeClaimsFingerprint(ClaimsIdentity identity) {
		var hash = new HashCode();
		hash.Add(identity.AuthenticationType, StringComparer.Ordinal);
		hash.Add(identity.NameClaimType, StringComparer.Ordinal);
		hash.Add(identity.RoleClaimType, StringComparer.Ordinal);

		foreach (var claim in identity.Claims
			.OrderBy(static c => c.Type, StringComparer.Ordinal)
			.ThenBy(static c => c.Value, StringComparer.Ordinal)) {
			hash.Add(claim.Type, StringComparer.Ordinal);
			hash.Add(claim.Value, StringComparer.Ordinal);
		}

		return hash.ToHashCode();
	}

	private static ClaimsPrincipal CreatePrincipal(
		TAccount account,
		RemoteAuthenticationUserOptions options) {

		ArgumentNullException.ThrowIfNull(account);
		ArgumentNullException.ThrowIfNull(options);

		// Deliberate fail-fast (stricter than the stock Blazor factory): a blank
		// AuthenticationType is a composition bug — the Cirreum auth composition verbs always
		// set it — and letting it through would surface as an inexplicable anonymous user
		// instead of a diagnosable configuration error.
		if (string.IsNullOrWhiteSpace(options.AuthenticationType)) {
			throw new InvalidOperationException(
				"Remote authentication type has not been configured. Set RemoteAuthenticationUserOptions.AuthenticationType (the Cirreum authentication composition verbs do this automatically).");
		}

		var identity = new ClaimsIdentity(
			options.AuthenticationType,
			options.NameClaim,
			options.RoleClaim);

		foreach (var (name, value) in account.AdditionalProperties) {
			if (value is null) {
				continue;
			}

			var claimValue = GetClaimValue(value);
			if (claimValue is null) {
				continue;
			}

			identity.AddClaim(new Claim(name, claimValue));
		}

		return new ClaimsPrincipal(identity);
	}

	private static string? GetClaimValue(object value) =>
		value switch {
			string text => text,

			JsonElement { ValueKind: JsonValueKind.String } json =>
				json.GetString(),

			JsonElement {
				ValueKind: JsonValueKind.Null or JsonValueKind.Undefined
			} => null,

			JsonElement json =>
				json.GetRawText(),

			// Extension data is always JsonElement, so this arm is a defensive fallback —
			// kept reflection-free (no JsonSerializer) for trimmed/AOT WASM publishing.
			_ => value.ToString()
		};

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