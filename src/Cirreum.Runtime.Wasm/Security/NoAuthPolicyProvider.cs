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

	public Task<AuthorizationPolicy> GetDefaultPolicyAsync() {
		return Task.FromResult(
			new AuthorizationPolicyBuilder()
				.RequireAssertion(_ => true)
				.Build()
		);
	}

	public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() {
		return Task.FromResult<AuthorizationPolicy?>(
			new AuthorizationPolicyBuilder()
				.RequireAssertion(_ => true)
				.Build()
		);
	}

	public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) {
		// Fail for known restricted policies or role-based policies
		if (RestrictedPolicies.Contains(policyName)) {
			return Task.FromResult<AuthorizationPolicy?>(
				new AuthorizationPolicyBuilder()
					.RequireAuthenticatedUser() // This will always fail for anonymous users
					.Build()
			);
		}

		// Allow all other policies
		return Task.FromResult<AuthorizationPolicy?>(
			new AuthorizationPolicyBuilder()
				.RequireAssertion(_ => true)
				.Build()
		);
	}
}