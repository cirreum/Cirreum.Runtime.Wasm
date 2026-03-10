namespace Cirreum.Runtime.Authentication;

using Cirreum.Runtime.Security;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

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
public abstract class CommonClaimsPrincipalFactory<TAccount>(
	ILogger logger,
	IServiceProvider serviceProvider,
	IAccessTokenProviderAccessor tokenAccessor,
	IEnumerable<IClaimsExtender>? claimsExtenders = null,
	IEnumerable<IAuthenticationPostProcessor>? postProcessors = null
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
	public override async ValueTask<ClaimsPrincipal> CreateUserAsync(
		TAccount account,
		RemoteAuthenticationUserOptions options) {

		if (account is null) {
#if DEBUG
			Console.WriteLine($"CommonClaimsPrincipalFactory => CreateUserAsync() - ACCOUNT IS NULL @ {DateTime.Now}");
#endif
			logger.LogCreateUser(AnonymousUser.AnonymousUserName);
			return this.SetAnonymous();
		}

#if DEBUG
		Console.WriteLine($"CommonClaimsPrincipalFactory => CreateUserAsync() - ACCOUNT {account} @ {DateTime.Now}");
#endif
		var userPrincipal = await base.CreateUserAsync(account, options);
		var userPrincipalName = userPrincipal.Identity?.Name ?? "unknown";
		logger.LogCreateUser(userPrincipalName);

		if (userPrincipal.Identity is ClaimsIdentity identity && identity.IsAuthenticated) {
			await this.OnAuthenticatedUser(userPrincipal, identity, account);
			return userPrincipal;
		}

		return this.SetAnonymous();
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
			stateManager?.NotifySubscribers<IClientUserState>(clientUser);
		} else {
			clientUser?.SetAnonymous();
		}

		return AnonymousUser.Shared;
	}

	// -------------------------------------------------------------------------
	// Authenticated Path
	// -------------------------------------------------------------------------

	private async Task OnAuthenticatedUser(
		ClaimsPrincipal userPrincipal,
		ClaimsIdentity identity,
		TAccount account) {

		if (!this.ShouldProcessPrincipal(userPrincipal)) {
#if DEBUG
			Console.WriteLine($"warn: CommonClaimsPrincipalFactory => SKIPPING CreateUserAsync() - ACCOUNT {account} @ {DateTime.Now} - same user within cooldown of {CooldownPeriod.TotalSeconds}s");
#endif
			return;
		}

		// Phase 1 — Inline, blocking, critical
		try {
			await this.MapIdentityAsync(identity, account);
			await this.ExtendClaimsAsync(identity, account);
			this.UpdatePrincipalTracking(userPrincipal);
		} catch (Exception e) {
			logger.LogCreateUserError(e);
			return;
		}

		var userName = userPrincipal.Identity?.Name ?? "unknown userName";
		try {

			// Phase 2 — Inline, blocking
			var clientUser = serviceProvider.GetRequiredService<ClientUser>();
			clientUser.SetAuthenticatedPrincipal(userPrincipal);

			// Phase 3 — Inline, blocking
			await this.RunPostProcessors(clientUser);

			var stateManager = serviceProvider.GetService<IStateManager>();
			if (stateManager is not null) {
				// Phase 4 — Best-effort deferred notification.
				_ = Task.Run(async () => {
					try {
						await stateManager.NotifySubscribersAsync<IClientUserState>(clientUser);
						logger.LogUserStateChanged(userName);
					} catch (Exception e) {
						logger.LogUserStateProcessingError(e, userName);
					}
				});
			}

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

	// -------------------------------------------------------------------------
	// Extension Points
	// -------------------------------------------------------------------------

	/// <summary>
	/// Override to provide custom claims mapping for the <see cref="ClaimsIdentity"/>.
	/// Called in Phase 1 — must complete before <c>CreateUserAsync</c> returns.
	/// </summary>
	protected abstract ValueTask MapIdentityAsync(ClaimsIdentity identity, TAccount account);

	/// <summary>
	/// Default implementation for calling each registered <see cref="IClaimsExtender"/>.
	/// Called in Phase 1 — must complete before <c>CreateUserAsync</c> returns.
	/// </summary>
	protected virtual async ValueTask ExtendClaimsAsync(ClaimsIdentity identity, TAccount account) {
		if (claimsExtenders is not null && claimsExtenders.Any()) {
			foreach (var extender in claimsExtenders.OrderBy(e => e.Order)) {
				try {
					await extender.ExtendClaimsAsync(identity, account, this.TokenProvider);
				} catch (Exception ex) {
					var extenderName = extender.GetType().FullName ?? "Unknown Extender";
					logger.LogClaimsExtenderError(ex, extenderName);
				}
			}
		}
	}

	/// <summary>
	/// Runs registered <see cref="IAuthenticationPostProcessor"/> instances against the
	/// authenticated user state. Called in Phase 2 — deferred after <c>CreateUserAsync</c> returns.
	/// </summary>
	private async Task RunPostProcessors(IUserState userState) {
		if (postProcessors is not null && postProcessors.Any()) {
			foreach (var processor in postProcessors.OrderBy(p => p.Order)) {
				try {
					await processor.ProcessAsync(serviceProvider, userState);
				} catch (Exception ex) {
					logger.LogWarning(ex, "Post-processor {ProcessorType} failed", processor.GetType().Name);
				}
			}
		}
	}

}