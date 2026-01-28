namespace Cirreum.Runtime.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Security.Claims;

sealed class NoAuthAuthenticationService : IAuthorizationService {

	public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) {
		// Fail if any requirement is role-based
		if (requirements.Any(r => r is RolesAuthorizationRequirement)) {
			return Task.FromResult(AuthorizationResult.Failed());
		}

		return Task.FromResult(AuthorizationResult.Success());
	}

	public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) {
		// Fail for known restrictive policies and role-based policies
		if (NoAuthPolicyProvider.RestrictedPolicies.Contains(policyName) || policyName.StartsWith("Role", StringComparison.OrdinalIgnoreCase)) {
			return Task.FromResult(AuthorizationResult.Failed());
		}

		return Task.FromResult(AuthorizationResult.Success());
	}

}