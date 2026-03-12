namespace Cirreum.Runtime.Authentication;

using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Security.Claims;

/// <summary>
/// Defines the contract for extending the claims collection during authentication.
/// </summary>
/// <remarks>
/// This interface is specifically focused on extending the claims at authentication time,
/// before the ClaimsPrincipal is fully constructed. It's separate from any UserProfile
/// enrichment that might happen after authentication is complete.
/// </remarks>
public interface IClaimsExtender {
	/// <summary>
	/// Determines the execution order of this extender. Extenders with lower
	/// values execute before extenders with higher values.
	/// </summary>
	int Order { get; }
	/// <summary>
	/// Extends the claims identity with additional claims.
	/// </summary>
	/// <param name="identity">The claims identity being constructed</param>
	/// <param name="account">The <typeparamref name="TAccount"/> with authentication information</param>
	void ExtendClaims<TAccount>(ClaimsIdentity identity, TAccount account)
		where TAccount : RemoteUserAccount;
}