namespace Cirreum.Runtime;

using Cirreum.Authorization;
using Cirreum.Runtime.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

public static partial class HostingExtensions {

	static bool _registered;

	/// <summary>
	/// Adds authorization services and registers the default application policies.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This method registers the following policies:
	/// <list type="bullet">
	/// <item>
	/// <term><see cref="AuthorizationPolicies.Standard"/></term>
	/// <description>Grants access to users with any valid role, from basic user to administrator.</description>
	/// </item>
	/// <item>
	/// <term><see cref="AuthorizationPolicies.StandardInternal"/></term>
	/// <description>Grants access to internal organization members and higher roles.</description>
	/// </item>
	/// <item>
	/// <term><see cref="AuthorizationPolicies.StandardAgent"/></term>
	/// <description>Grants access to service agents and higher roles.</description>
	/// </item>
	/// <item>
	/// <term><see cref="AuthorizationPolicies.StandardManager"/></term>
	/// <description>Grants access to managers and higher roles.</description>
	/// </item>
	/// <item>
	/// <term><see cref="AuthorizationPolicies.StandardAdmin"/></term>
	/// <description>Grants access exclusively to administrators.</description>
	/// </item>
	/// </list>
	/// </para>
	/// <para>
	/// Note: The system role is excluded from client-side policies as it is not applicable in interactive contexts.
	/// </para>
	/// </remarks>
	/// <param name="builder">The <see cref="IClientDomainApplicationBuilder"/> to which the authorization services are added.</param>
	/// <param name="authorization">Optional callback to configure additional authorization options.</param>
	public static void AddDefaultAuthorization(this IClientDomainApplicationBuilder builder,
		Action<AuthorizationOptions>? authorization = null) {

		if (_registered) {
			return;
		}
		_registered = true;

		if (!builder.Services.Any(d => d.ServiceType == typeof(AuthenticationStateProvider))) {
			// allow all users when no-auth is configured
			builder.Services.AddScoped<AuthenticationStateProvider, NoAuthStateProvider>();
			builder.Services.AddScoped<IAuthorizationPolicyProvider, NoAuthPolicyProvider>();
			builder.Services.AddScoped<IAuthorizationService, NoAuthAuthenticationService>();
			builder.Services.AddScoped<IUserStateAccessor, NoAuthUserStateAccessor>();
			return;
		}

		builder.Services.AddAuthorizationCore(authOptions => {

			// Standard - Any valid user role
			authOptions.AddPolicy(AuthorizationPolicies.Standard, policy =>
				policy
					.RequireAuthenticatedUser()
					.RequireRole(
						ApplicationRoles.AppAdminRole,
						ApplicationRoles.AppManagerRole,
						ApplicationRoles.AppAgentRole,
						ApplicationRoles.AppInternalRole,
						ApplicationRoles.AppUserRole));

			// Internal organization access
			authOptions.AddPolicy(AuthorizationPolicies.StandardInternal, policy =>
				policy
					.RequireAuthenticatedUser()
					.RequireRole(
						ApplicationRoles.AppAdminRole,
						ApplicationRoles.AppManagerRole,
						ApplicationRoles.AppInternalRole));

			// Agent/support access
			authOptions.AddPolicy(AuthorizationPolicies.StandardAgent, policy =>
				policy
					.RequireAuthenticatedUser()
					.RequireRole(
						ApplicationRoles.AppAdminRole,
						ApplicationRoles.AppManagerRole,
						ApplicationRoles.AppAgentRole));

			// Management access
			authOptions.AddPolicy(AuthorizationPolicies.StandardManager, policy =>
				policy
					.RequireAuthenticatedUser()
					.RequireRole(
						ApplicationRoles.AppAdminRole,
						ApplicationRoles.AppManagerRole));

			// Admin only access
			authOptions.AddPolicy(AuthorizationPolicies.StandardAdmin, policy =>
				policy
					.RequireAuthenticatedUser()
					.RequireRole(ApplicationRoles.AppAdminRole));

			// Allow additional custom policies
			if (authorization is not null) {
				authorization(authOptions);
			}
		});

	}

}