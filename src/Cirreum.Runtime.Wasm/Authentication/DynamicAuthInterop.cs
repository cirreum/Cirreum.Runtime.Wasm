namespace Cirreum.Runtime.Authentication;

using Microsoft.JSInterop.WebAssembly;
using System.ComponentModel;

/// <summary>
/// Provides synchronous JavaScript interop for reading authentication and tenant
/// configuration. Used during application startup and initialization.
/// </summary>
/// <remarks>
/// <para>
/// <strong>⚠️ Internal infrastructure—not intended for direct use in application code.</strong>
/// </para>
/// <para>
/// Used by <c>Cirreum.Runtime.Wasm.Msal</c> and <c>Cirreum.Runtime.Wasm.Oidc</c>
/// for <c>AddDynamicAuth()</c>, and by startup tasks to configure runtime services.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DynamicAuthInterop {

	private static readonly Lazy<BootstrapJSRuntime> LazyRuntime = new(
		static () => new BootstrapJSRuntime(),
		LazyThreadSafetyMode.ExecutionAndPublication);

	private static BootstrapJSRuntime Runtime => LazyRuntime.Value;

	/// <summary>
	/// Gets the tenant authentication configuration set by the loader.
	/// </summary>
	/// <returns>The tenant auth config, or null if not in dynamic auth mode.</returns>
	public static TenantAuthConfig? GetTenantAuthConfig() {
		return Invoke<TenantAuthConfig?>("cirreum.tenant.getConfig");
	}

	/// <summary>
	/// Gets the tenant slug extracted from the URL path.
	/// </summary>
	/// <returns>The tenant slug, or "default" if not determined.</returns>
	public static string GetTenantSlug() {
		return Invoke<string?>("cirreum.tenant.getSlug") ?? "default";
	}

	/// <summary>
	/// Gets the tenant display name.
	/// </summary>
	/// <returns>The display name, or null if not available.</returns>
	public static string? GetTenantDisplayName() {
		return Invoke<string?>("cirreum.tenant.getDisplayName");
	}

	/// <summary>
	/// Gets the declared auth mode from the script attribute.
	/// </summary>
	/// <returns>"dynamic", "msal", "oidc", or "none".</returns>
	public static string GetAuthMode() {
		return Invoke<string?>("cirreum.auth.getMode") ?? "none";
	}

	/// <summary>
	/// Gets the resolved authentication library as a string.
	/// </summary>
	/// <returns>"msal", "oidc", or "none".</returns>
	public static string GetAuthLibrary() {
		return Invoke<string?>("cirreum.auth.getLibrary") ?? "none";
	}

	/// <summary>
	/// Gets the resolved authentication library as a typed enum.
	/// </summary>
	/// <returns>The <see cref="AuthenticationLibraryType"/>, or null if auth is disabled.</returns>
	public static AuthenticationLibraryType? GetAuthLibraryType() {
		var library = GetAuthLibrary();
		return library.ToLowerInvariant() switch {
			"msal" => AuthenticationLibraryType.MSAL,
			"oidc" => AuthenticationLibraryType.OIDC,
			_ => null
		};
	}

	/// <summary>
	/// Checks if authentication is enabled.
	/// </summary>
	/// <returns>True if an auth library is loaded.</returns>
	public static bool IsAuthEnabled() {
		return Invoke<bool>("cirreum.auth.isEnabled");
	}

	private static TResult? Invoke<TResult>(string identifier, params object?[]? args) {
		return Runtime.Invoke<TResult>(identifier, args);
	}

	private sealed class BootstrapJSRuntime : WebAssemblyJSRuntime { }
}