namespace Cirreum.Runtime.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Security.Claims;

sealed class NoAuthAuthenticationService : IAuthorizationService {

	private static readonly Task<AuthorizationResult> _success =
		Task.FromResult(AuthorizationResult.Success());
	private static readonly Task<AuthorizationResult> _failed =
		Task.FromResult(AuthorizationResult.Failed());

	public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) {
		if (requirements.Any(r => r is RolesAuthorizationRequirement)) {
			return _failed;
		}
		return _success;
	}

	public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) {
		if (NoAuthPolicyProvider.RestrictedPolicies.Contains(policyName) || policyName.StartsWith("Role", StringComparison.OrdinalIgnoreCase)) {
			return _failed;
		}
		return _success;
	}

}