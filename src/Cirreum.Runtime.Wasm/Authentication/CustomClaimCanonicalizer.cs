namespace Cirreum.Runtime.Authentication;

using System.Security.Claims;
using System.Text.Json;

/// <summary>
/// Aliases provisioned <c>custom*</c> token claims to their native names during authentication —
/// <c>customRoles</c> → <c>roles</c>, <c>customName</c> → <c>name</c>, and so on.
/// </summary>
/// <remarks>
/// <para>
/// This is the client counterpart to the server-side provisioning mint: an application projects
/// its claims into the token under a <c>custom*</c> namespace (collision-safe against native
/// identity-provider claims), and this step aliases each one back to the native claim the rest of
/// the client reads — <see cref="ClaimsPrincipal.IsInRole(string)"/>, the configured name / role
/// claim types, and any <see cref="IClaimsExtender"/>. The two named claims (<c>customRoles</c>,
/// <c>customName</c>) alias to the identity's own <see cref="ClaimsIdentity.RoleClaimType"/> /
/// <see cref="ClaimsIdentity.NameClaimType"/> — whatever the provider configured — so
/// <see cref="ClaimsPrincipal.IsInRole(string)"/> and <see cref="System.Security.Principal.IIdentity.Name"/>
/// resolve correctly even when those are not literally <c>roles</c> / <c>name</c>.
/// </para>
/// <para>
/// The step is purely <b>additive</b> and <b>idempotent</b>: it never removes a claim, and it
/// never adds an exact <c>(type, value)</c> duplicate — running it a second time is a no-op. A
/// <c>custom*</c> array claim (which arrives as a single JSON-array string) is split so each value
/// becomes its own native claim, as <see cref="ClaimsPrincipal.IsInRole(string)"/> requires. An
/// alias preserves the source claim's issuer, original issuer, and properties, with
/// <see cref="ClaimValueTypes.String"/> as its value type — an expanded element is a scalar
/// string regardless of how the source array claim was typed. When both a native claim and its
/// provisioned <c>custom*</c> counterpart are present, both survive — the framework cannot know
/// whether the native value is stale-and-to-be-replaced or complementary, so it does not guess.
/// Resolving that precedence is the application's decision, made in its own
/// <see cref="IClaimsExtender"/> (which runs after this step, with full visibility of every claim).
/// </para>
/// <para>
/// Value policy mirrors the provisioning contract — provisioned values are non-blank strings, and
/// provisioned arrays are string arrays: a null, empty, or non-string array entry is dropped, and
/// an empty scalar produces no alias.
/// </para>
/// <para>
/// It is inert when the token carries no <c>custom*</c> claims — for example when the identity
/// provider already canonicalized them — so it is always safe to run.
/// </para>
/// </remarks>
internal static class CustomClaimCanonicalizer {

	private const string Prefix = "custom";

	// The two native claim names the framework treats specially — the client counterpart to the
	// server's CustomClaimNames.Roles / .Name (whose native forms are "roles" / "name"). They
	// resolve to the identity's configured role / name claim type rather than a literal, so IsInRole
	// and Identity.Name work under any provider config.
	private const string NativeRoles = "roles";
	private const string NativeName = "name";

	/// <summary>
	/// Adds a native-named alias for every <c>custom*</c> claim on <paramref name="identity"/>,
	/// splitting JSON-array values into individual claims. Existing claims are left untouched, and
	/// an exact <c>(type, value)</c> duplicate is never added — the operation is idempotent.
	/// </summary>
	/// <param name="identity">The claims identity being constructed.</param>
	internal static void Canonicalize(ClaimsIdentity identity) {
		ArgumentNullException.ThrowIfNull(identity);

		// Snapshot: we add claims to the identity while iterating its current custom* claims.
		var customClaims = identity.Claims.Where(IsCustomPrefixed).ToList();
		if (customClaims.Count == 0) {
			return;
		}

		// Idempotency: track every (type, value) pair already on the identity so a repeated run —
		// or an alias whose target type is itself custom* (options.RoleClaim = "customRoles") —
		// never adds a duplicate claim.
		var existing = new HashSet<(string Type, string Value)>(
			identity.Claims.Select(static claim => (claim.Type, claim.Value)));

		foreach (var claim in customClaims) {
			var claimType = NativeClaimType(claim.Type, identity);

			foreach (var value in ExpandValues(claim.Value)) {
				if (!existing.Add((claimType, value))) {
					continue;
				}

				// Preserve provenance (issuer / original issuer / properties). The value type is
				// pinned to String — an expanded element is a scalar string regardless of how the
				// source array claim was typed.
				var alias = new Claim(
					claimType,
					value,
					ClaimValueTypes.String,
					claim.Issuer,
					claim.OriginalIssuer,
					identity);

				foreach (var property in claim.Properties) {
					alias.Properties[property.Key] = property.Value;
				}

				identity.AddClaim(alias);
			}
		}
	}

	// Convert the custom* wire name to its native name, then resolve the two named claims to the
	// identity's configured claim type (roles -> RoleClaimType, name -> NameClaimType) so IsInRole
	// and Identity.Name resolve whatever the provider named them; every other native name is used
	// as-is (customTenant -> tenant).
	private static string NativeClaimType(string wireName, ClaimsIdentity identity) {
		var native = ToNativeName(wireName);
		return native switch {
			NativeRoles => identity.RoleClaimType,
			NativeName => identity.NameClaimType,
			_ => native
		};
	}

	// A custom-prefixed claim is "custom" followed by an upper-cased first character — the server's
	// ToWireName guarantees the capital — so "customRoles" / "customName" match while an unrelated
	// "customer" or "customs" claim is left alone.
	private static bool IsCustomPrefixed(Claim claim) =>
		claim.Type.Length > Prefix.Length
			&& claim.Type.StartsWith(Prefix, StringComparison.Ordinal)
			&& char.IsUpper(claim.Type[Prefix.Length]);

	// Strip the prefix and lower-case the first remaining character: customRoles -> roles.
	private static string ToNativeName(string type) =>
		char.ToLowerInvariant(type[Prefix.Length]) + type[(Prefix.Length + 1)..];

	// A custom* array claim arrives as a single JSON-array string (["admin","user"]); split it so
	// each value becomes its own claim. Provisioned arrays are contractually non-blank STRING
	// arrays (the server-side IdentityClaim rejects blank values at construction), so null, empty,
	// and non-string entries are dropped rather than minted as claim values. Anything that is not
	// a JSON array passes through as an opaque scalar; an empty scalar produces no alias.
	private static IEnumerable<string> ExpandValues(string value) {
		if (string.IsNullOrEmpty(value)) {
			return [];
		}

		var candidate = value.AsSpan().TrimStart();
		if (candidate.IsEmpty || candidate[0] != '[') {
			return [value];
		}

		try {
			using var doc = JsonDocument.Parse(value);
			if (doc.RootElement.ValueKind == JsonValueKind.Array) {
				return [.. doc.RootElement.EnumerateArray()
					.Where(static e => e.ValueKind == JsonValueKind.String)
					.Select(static e => e.GetString())
					.Where(static s => !string.IsNullOrEmpty(s))
					.Select(static s => s!)];
			}
		} catch (JsonException) {
			// Not valid JSON — treat as an opaque scalar value.
		}

		return [value];
	}

}
