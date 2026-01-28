namespace Cirreum.Runtime.Authentication;

using Cirreum.Runtime.Security;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

/// <summary>
/// Parses the newly created claims principal and sync's IUserState.
/// </summary>
/// <param name="tokenAccessor"></param>
/// <param name="serviceProvider"></param>
/// <param name="logger"></param>
/// <param name="claimsExtenders">The optional collection of <see cref="IClaimsExtender"/>s</param>
/// <param name="postProcessors"></param>
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

	private AnonymousUser SetAnonymous() {
		this._lastProcessedTime = DateTimeOffset.MinValue;
		this._lastProcessedId = null;
		this._lastProcessedName = null;
		// only notify subscribers if the user was previously authenticated
		var stateManager = serviceProvider.GetService<IStateManager>();
		var clientUser = serviceProvider.GetService<IUserState>() as ClientUser;
		if (clientUser?.IsAuthenticated == true) {
			clientUser.SetAnonymous();
			stateManager?.NotifySubscribers(clientUser);
		} else {
			clientUser?.SetAnonymous();
		}
		return AnonymousUser.Shared;
	}
	private bool ShouldProcessPrincipal(ClaimsPrincipal principal) {
		var id = ClaimsHelper.ResolveId(principal) ?? "";
		var name = ClaimsHelper.ResolveName(principal) ?? "";
		var now = DateTimeOffset.Now;

		// Process if any of these conditions are true:
		// 1. User identity has changed (different person)
		// 2. Cooldown period has expired (prevents rapid duplicate processing)
		return id != this._lastProcessedId ||
			   name != this._lastProcessedName ||
			   now >= _lastProcessedTime + CooldownPeriod;
	}
	private void UpdatePrincipalTracking(ClaimsPrincipal principal) {
		this._lastProcessedId = ClaimsHelper.ResolveId(principal) ?? "";
		this._lastProcessedName = ClaimsHelper.ResolveName(principal) ?? "";
		this._lastProcessedTime = DateTimeOffset.Now;
	}

	/// <summary>
	/// Handle newly authenticated user
	/// </summary>
	/// <param name="account"></param>
	/// <param name="options"></param>
	/// <returns></returns>
	public override async ValueTask<ClaimsPrincipal> CreateUserAsync(TAccount account, RemoteAuthenticationUserOptions options) {

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

		// Process Authenticated Principal Identity
		if (userPrincipal.Identity is ClaimsIdentity identity && identity.IsAuthenticated) {
			await this.OnAuthenticatedUser(userPrincipal, identity, account);
			return userPrincipal;
		}

		return this.SetAnonymous();

	}
	private async Task OnAuthenticatedUser(ClaimsPrincipal userPrincipal, ClaimsIdentity identity, TAccount account) {

		if (!this.ShouldProcessPrincipal(userPrincipal)) {
#if DEBUG
			Console.WriteLine($"warn: CommonClaimsPrincipalFactory => SKIPPING CreateUserAsync() - ACCOUNT {account} @ {DateTime.Now} - same user within the cooldown period of {CooldownPeriod.TotalSeconds}s");
#endif
			return;
		}

		// Phase 1: Principal/Identity Processing (Critical - must succeed)
		try {
			await this.MapIdentityAsync(identity, account);
			await this.ExtendClaimsAsync(identity, account);
			this.UpdatePrincipalTracking(userPrincipal);
		} catch (Exception e) {
			logger.LogCreateUserError(e);
			return; // Exit early - can't proceed without valid principal
		}

		// Phase 2: User State & Notifications (Best effort - authentication already succeeded)
		try {

			var clientUser = serviceProvider.GetRequiredService<ClientUser>();
			clientUser.SetAuthenticatedPrincipal(userPrincipal);
			await this.RunPostProcessors(clientUser);

			var stateManager = serviceProvider.GetService<IStateManager>();
			if (stateManager is not null) {
				_ = Task.Run(() => {
					try {
						stateManager.NotifySubscribers(clientUser);
						logger.LogUserStateChanged(clientUser.Name);
					} catch (Exception ex) {
						logger.LogStateNotificationError(ex, clientUser.Name);
					}
				});
			}
		} catch (Exception e) {
			logger.LogUserStateProcessingError(e, userPrincipal?.Identity?.Name ?? "unknown");
		}

	}


	/// <summary>
	/// Override to provide custom claims mapping for the <see cref="ClaimsIdentity"/>.
	/// </summary>
	/// <param name="identity"></param>
	/// <param name="account"></param>
	/// <returns></returns>
	protected abstract ValueTask MapIdentityAsync(ClaimsIdentity identity, TAccount account);
	/// <summary>
	/// Default implementation for calling each registered claims extender.
	/// </summary>
	/// <param name="identity">The current <see cref="ClaimsIdentity"/>.</param>
	/// <param name="account">The current <typeparamref name="TAccount"/> instance.</param>
	protected virtual async ValueTask ExtendClaimsAsync(ClaimsIdentity identity, TAccount account) {
		// Allow extenders to extend the claims
		if (claimsExtenders is not null && claimsExtenders.Any()) {
			foreach (var extender in claimsExtenders.OrderBy(e => e.Order)) {
				try {
					await extender.ExtendClaimsAsync(identity, account, this.TokenProvider);
				} catch (Exception ex) {
					// Log the error but continue with other extenders
					var extenderName = extender.GetType().FullName ?? "Unknown Extender";
					logger.LogClaimsExtenderError(ex, extenderName);
				}
			}
		}
	}
	private async Task RunPostProcessors(IUserState userState) {
		if (postProcessors is not null && postProcessors.Any()) {
			foreach (var processor in postProcessors.OrderBy(p => p.Order)) {
				try {
					await processor.ProcessAsync(serviceProvider, userState);
				} catch (Exception ex) {
					if (logger.IsEnabled(LogLevel.Warning)) {
						var processName = processor.GetType().Name;
						logger.LogWarning(ex, "Post-processor {ProcessorType} failed", processName);
					}
				}
			}
		}
	}

}