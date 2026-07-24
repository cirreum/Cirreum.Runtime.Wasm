namespace Cirreum.Runtime.Wasm.Tests;

public class CustomClaimCanonicalizerTests {

	private static ClaimsIdentity IdentityWith(params Claim[] claims) =>
		new(claims, authenticationType: "test", nameType: "name", roleType: "roles");

	[Fact]
	public void A_customRoles_array_becomes_individual_role_claims() {
		var identity = IdentityWith(new Claim("customRoles", """["admin","subscriber"]"""));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("roles").Select(c => c.Value).Should().BeEquivalentTo("admin", "subscriber");
		new ClaimsPrincipal(identity).IsInRole("admin").Should().BeTrue();
		new ClaimsPrincipal(identity).IsInRole("subscriber").Should().BeTrue();
	}

	[Fact]
	public void A_single_element_role_array_becomes_one_role_claim() {
		var identity = IdentityWith(new Claim("customRoles", """["admin"]"""));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("roles").Select(c => c.Value).Should().ContainSingle().Which.Should().Be("admin");
	}

	[Fact]
	public void A_scalar_customName_becomes_a_single_name_claim() {
		var identity = IdentityWith(new Claim("customName", "Jane Smith"));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("name").Select(c => c.Value).Should().ContainSingle().Which.Should().Be("Jane Smith");
		new ClaimsPrincipal(identity).Identity!.Name.Should().Be("Jane Smith");
	}

	[Fact]
	public void An_arbitrary_custom_claim_is_aliased_to_its_native_name() {
		var identity = IdentityWith(new Claim("customTenant", "acme"));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("tenant").Select(c => c.Value).Should().ContainSingle().Which.Should().Be("acme");
	}

	[Fact]
	public void The_original_custom_claim_is_preserved() {
		var identity = IdentityWith(new Claim("customName", "Jane Smith"));

		CustomClaimCanonicalizer.Canonicalize(identity);

		// Additive: the source claim survives alongside the native alias.
		identity.FindAll("customName").Should().ContainSingle();
		identity.FindAll("name").Should().ContainSingle();
	}

	[Fact]
	public void A_native_claim_is_never_removed_and_the_minted_value_is_unioned_in() {
		// The framework does not guess precedence — both the native role and the minted role survive;
		// resolving a conflict is the app's IClaimsExtender's job.
		var identity = IdentityWith(
			new Claim("roles", "reader"),
			new Claim("customRoles", """["admin"]"""));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("roles").Select(c => c.Value).Should().BeEquivalentTo("reader", "admin");
	}

	[Fact]
	public void A_lookalike_claim_that_is_not_custom_cased_is_left_untouched() {
		// "customer" is "custom" + a lower-case char, so it is not a minted claim.
		var identity = IdentityWith(new Claim("customer", "n-1934"));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("er").Should().BeEmpty();
		identity.FindAll("customer").Should().ContainSingle();
	}

	[Fact]
	public void The_bare_prefix_with_no_suffix_is_left_untouched() {
		var identity = IdentityWith(new Claim("custom", "value"));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.Claims.Should().ContainSingle().Which.Type.Should().Be("custom");
	}

	[Fact]
	public void An_identity_with_no_custom_claims_is_unchanged() {
		var identity = IdentityWith(
			new Claim("name", "Jane"),
			new Claim("roles", "reader"));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.Claims.Should().HaveCount(2);
	}

	[Fact]
	public void A_custom_value_that_only_looks_like_an_array_falls_through_as_a_scalar() {
		var identity = IdentityWith(new Claim("customNote", "[not valid json"));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("note").Select(c => c.Value).Should().ContainSingle().Which.Should().Be("[not valid json");
	}

	[Fact]
	public void CustomRoles_aliases_to_the_identitys_configured_role_claim_type() {
		// The provider (e.g. an OIDC app via AddOidcAuth(roleClaimType: "role")) may configure a
		// non-default role claim type; customRoles must follow it so IsInRole resolves.
		var identity = new ClaimsIdentity(
			[new Claim("customRoles", """["admin"]""")],
			authenticationType: "test", nameType: "name", roleType: "role");

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("role").Select(c => c.Value).Should().ContainSingle().Which.Should().Be("admin");
		identity.FindAll("roles").Should().BeEmpty();
		new ClaimsPrincipal(identity).IsInRole("admin").Should().BeTrue();
	}

	[Fact]
	public void CustomName_aliases_to_the_identitys_configured_name_claim_type() {
		var identity = new ClaimsIdentity(
			[new Claim("customName", "Jane Smith")],
			authenticationType: "test", nameType: "preferred_username", roleType: "roles");

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("preferred_username").Select(c => c.Value).Should().ContainSingle().Which.Should().Be("Jane Smith");
		new ClaimsPrincipal(identity).Identity!.Name.Should().Be("Jane Smith");
	}

	[Fact]
	public void A_non_special_custom_claim_uses_its_literal_derived_name() {
		// Only customRoles / customName follow the configured claim types; customTenant is literal.
		var identity = new ClaimsIdentity(
			[new Claim("customTenant", "acme")],
			authenticationType: "test", nameType: "preferred_username", roleType: "role");

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("tenant").Select(c => c.Value).Should().ContainSingle().Which.Should().Be("acme");
	}

	[Fact]
	public void Canonicalize_is_idempotent() {
		var identity = IdentityWith(
			new Claim("roles", "reader"),
			new Claim("customRoles", """["admin","subscriber"]"""),
			new Claim("customName", "Jane Smith"),
			new Claim("customTenant", "acme"));

		CustomClaimCanonicalizer.Canonicalize(identity);
		var afterFirstRun = identity.Claims.Select(c => (c.Type, c.Value)).ToList();

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.Claims.Select(c => (c.Type, c.Value)).Should().Equal(afterFirstRun);
	}

	[Fact]
	public void An_alias_value_the_identity_already_carries_is_not_duplicated() {
		// The token natively carries roles:admin AND the mint carries the same value — the alias
		// would be an exact (type, value) duplicate, so it is skipped. Still additive: nothing
		// is removed, and the distinct minted value is unioned in.
		var identity = IdentityWith(
			new Claim("roles", "admin"),
			new Claim("customRoles", """["admin","subscriber"]"""));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("roles").Select(c => c.Value).Should().BeEquivalentTo("admin", "subscriber");
	}

	[Fact]
	public void A_role_claim_type_configured_as_customRoles_itself_splits_once_and_stays_stable() {
		// An app may point options.RoleClaim directly at the wire name. The alias target is then
		// itself a custom* type; the array still splits so IsInRole works, and a second run
		// must not re-expand what the first run added.
		var identity = new ClaimsIdentity(
			[new Claim("customRoles", """["admin","subscriber"]""")],
			authenticationType: "test", nameType: "name", roleType: "customRoles");

		CustomClaimCanonicalizer.Canonicalize(identity);
		var afterFirstRun = identity.Claims.Select(c => (c.Type, c.Value)).ToList();

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.Claims.Select(c => (c.Type, c.Value)).Should().Equal(afterFirstRun);
		new ClaimsPrincipal(identity).IsInRole("admin").Should().BeTrue();
		new ClaimsPrincipal(identity).IsInRole("subscriber").Should().BeTrue();
	}

	[Fact]
	public void Blank_array_entries_are_dropped() {
		var identity = IdentityWith(new Claim("customRoles", """["admin","",null,"user"]"""));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("roles").Select(c => c.Value).Should().BeEquivalentTo("admin", "user");
	}

	[Fact]
	public void Non_string_array_entries_are_dropped() {
		// Provisioned arrays are contractually string arrays — a boolean, number, or object must
		// never become a role value.
		var identity = IdentityWith(new Claim("customRoles", """["admin",true,12,{"scope":"read"}]"""));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("roles").Select(c => c.Value).Should().ContainSingle().Which.Should().Be("admin");
	}

	[Fact]
	public void An_array_with_leading_whitespace_still_expands() {
		var identity = IdentityWith(new Claim("customRoles", """  ["admin","user"]"""));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("roles").Select(c => c.Value).Should().BeEquivalentTo("admin", "user");
	}

	[Fact]
	public void An_empty_scalar_produces_no_alias() {
		// Provisioned values are contractually non-blank; an empty custom* scalar mints nothing.
		var identity = IdentityWith(new Claim("customTenant", ""));

		CustomClaimCanonicalizer.Canonicalize(identity);

		identity.FindAll("tenant").Should().BeEmpty();
		identity.FindAll("customTenant").Should().ContainSingle();
	}

	[Fact]
	public void An_alias_preserves_the_source_claims_provenance() {
		var source = new Claim("customTenant", "acme", "urn:custom:type", "https://idp.example", "https://original.example");
		source.Properties["flow"] = "signup";
		var identity = IdentityWith(source);

		CustomClaimCanonicalizer.Canonicalize(identity);

		var alias = identity.FindFirst("tenant")!;
		alias.Issuer.Should().Be("https://idp.example");
		alias.OriginalIssuer.Should().Be("https://original.example");
		alias.Properties.Should().ContainKey("flow").WhoseValue.Should().Be("signup");
		// Value type is pinned to String — an expanded element is a scalar string regardless of
		// how the source claim was typed.
		alias.ValueType.Should().Be(ClaimValueTypes.String);
	}
}
