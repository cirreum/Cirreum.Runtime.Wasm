namespace Cirreum.Runtime.Security;

using Cirreum.Authorization;
using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Authorization policy provider that allows all requests as anonymous/guest access.
/// Used when OIDC/MSAL authentication is not configured in the application.
/// </summary>
sealed class NoAuthPolicyProvider : IAuthorizationPolicyProvider {

	internal static readonly HashSet<string> RestrictedPolicies
		= new(AuthorizationPolicies.All, StringComparer.OrdinalIgnoreCase);

	private static readonly AuthorizationPolicy _allowAllPolicy =
		new AuthorizationPolicyBuilder()
			.RequireAssertion(_ => true)
			.Build();

	private static readonly AuthorizationPolicy _restrictedPolicy =
		new AuthorizationPolicyBuilder()
			.RequireAuthenticatedUser()
			.Build();

	private static readonly Task<AuthorizationPolicy> _defaultPolicy =
		Task.FromResult(_allowAllPolicy);

	private static readonly Task<AuthorizationPolicy?> _allowAllPolicyNullable =
		Task.FromResult<AuthorizationPolicy?>(_allowAllPolicy);

	private static readonly Task<AuthorizationPolicy?> _restrictedPolicyNullable =
		Task.FromResult<AuthorizationPolicy?>(_restrictedPolicy);

	public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _defaultPolicy;

	public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _allowAllPolicyNullable;

	public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) {
		if (RestrictedPolicies.Contains(policyName)) {
			return _restrictedPolicyNullable;
		}
		return _allowAllPolicyNullable;
	}

}