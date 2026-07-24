namespace Cirreum.Runtime.Wasm.Tests;

using Cirreum.Runtime.Security;
using Cirreum.Security;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

public class CommonClaimsPrincipalFactoryTests {

	// ---------------------------------------------------------------------
	// Harness
	// ---------------------------------------------------------------------

	private sealed class FakeAccessor : IAccessTokenProviderAccessor {
		public IAccessTokenProvider TokenProvider => null!;
	}

	// Emulates a provider factory (MsalClaimsPrincipalFactory maps EntraUserAccount.Roles
	// exactly like this — roles exist ONLY through MapIdentity for MSAL).
	private sealed class TestFactory(
		IServiceProvider services,
		IReadOnlyList<string>? mappedRoles = null,
		IEnumerable<IClaimsExtender>? extenders = null,
		bool throwInMapIdentity = false)
		: CommonClaimsPrincipalFactory<RemoteUserAccount>(
			NullLogger.Instance, services, new FakeAccessor(), extenders) {

		public bool ThrowInMapIdentity { get; set; } = throwInMapIdentity;

		protected override void MapIdentity(ClaimsIdentity identity, RemoteUserAccount account) {
			if (this.ThrowInMapIdentity) {
				throw new InvalidOperationException("MapIdentity failed.");
			}
			foreach (var role in mappedRoles ?? []) {
				identity.AddClaim(new Claim("roles", role));
			}
		}
	}

	private static ServiceProvider NewServices() =>
		new ServiceCollection().AddSingleton<ClientUser>().BuildServiceProvider();

	private static RemoteUserAccount AccountWith(params (string Name, string RawJson)[] members) {
		var account = new RemoteUserAccount { AdditionalProperties = new Dictionary<string, object>() };
		foreach (var (name, rawJson) in members) {
			account.AdditionalProperties[name] = JsonDocument.Parse(rawJson).RootElement.Clone();
		}
		return account;
	}

	private static RemoteAuthenticationUserOptions Options() =>
		new() { AuthenticationType = "test", NameClaim = "name", RoleClaim = "roles" };

	// ---------------------------------------------------------------------
	// CreatePrincipal — claim-value conversion
	// ---------------------------------------------------------------------

	[Fact]
	public async Task String_members_become_unquoted_claim_values() {
		var factory = new TestFactory(NewServices());
		var account = AccountWith(
			("sub", """ "user-1" """),
			("name", """ "Jane Smith" """));

		var principal = await factory.CreateUserAsync(account, Options());

		principal.FindFirst("sub")!.Value.Should().Be("user-1");
		principal.FindFirst("name")!.Value.Should().Be("Jane Smith");
		principal.Identity!.Name.Should().Be("Jane Smith");
	}

	[Fact]
	public async Task Json_null_members_do_not_become_claims() {
		var factory = new TestFactory(NewServices());
		var account = AccountWith(
			("sub", """ "user-1" """),
			("middle_name", "null"));

		var principal = await factory.CreateUserAsync(account, Options());

		principal.FindFirst("middle_name").Should().BeNull();
	}

	[Fact]
	public async Task Array_members_keep_their_raw_json_and_canonicalize_through_the_pipeline() {
		var factory = new TestFactory(NewServices());
		var account = AccountWith(
			("sub", """ "user-1" """),
			("customRoles", """ ["admin","subscriber"] """),
			("customName", """ "Jane Smith" """));

		var principal = await factory.CreateUserAsync(account, Options());

		// The raw custom* claims survive (additive), and the canonicalized natives are usable.
		principal.FindFirst("customRoles")!.Value.Should().Be("""["admin","subscriber"]""");
		principal.IsInRole("admin").Should().BeTrue();
		principal.IsInRole("subscriber").Should().BeTrue();
		principal.Identity!.Name.Should().Be("Jane Smith");
	}

	// ---------------------------------------------------------------------
	// Deduplication window — transforms always run, only publication dedupes
	// ---------------------------------------------------------------------

	[Fact]
	public async Task A_duplicate_call_within_the_window_still_returns_a_fully_processed_principal() {
		var factory = new TestFactory(NewServices(), mappedRoles: ["admin"]);
		var account = AccountWith(
			("sub", """ "user-1" """),
			("name", """ "Jane Smith" """),
			("customTenant", """ "acme" """));

		var first = await factory.CreateUserAsync(account, Options());
		var second = await factory.CreateUserAsync(account, Options());

		// Pre-fix, the second (deduplicated) call skipped MapIdentity + canonicalization and
		// handed Blazor a principal with no roles at all.
		first.IsInRole("admin").Should().BeTrue();
		second.IsInRole("admin").Should().BeTrue();
		second.FindFirst("tenant")!.Value.Should().Be("acme");
	}

	[Fact]
	public async Task A_duplicate_call_within_the_window_skips_state_republication() {
		var services = NewServices();
		var factory = new TestFactory(services);
		var clientUser = services.GetRequiredService<ClientUser>();
		var account = AccountWith(
			("sub", """ "user-1" """),
			("name", """ "Jane Smith" """));

		var first = await factory.CreateUserAsync(account, Options());
		var profileAfterFirst = clientUser.Profile;

		var second = await factory.CreateUserAsync(account, Options());

		// The duplicate call must not rebuild UserProfile / re-set the ClientUser principal.
		clientUser.Profile.Should().BeSameAs(profileAfterFirst);
		clientUser.Principal.Should().BeSameAs(first);
		second.Should().NotBeSameAs(first);
	}

	[Fact]
	public async Task The_dedup_key_is_stable_when_the_name_arrives_only_via_customName() {
		var services = NewServices();
		var factory = new TestFactory(services);
		var clientUser = services.GetRequiredService<ClientUser>();
		// Thin IaaS token: no native name — the display name is minted as customName.
		var account = AccountWith(
			("sub", """ "user-1" """),
			("customName", """ "Jane Smith" """));

		await factory.CreateUserAsync(account, Options());
		var profileAfterFirst = clientUser.Profile;

		await factory.CreateUserAsync(account, Options());

		// Pre-fix, the key was read from the RAW principal (no name yet) but stored from the
		// PROCESSED one ("Jane Smith") — never equal, so the window never engaged and every
		// duplicate call re-published state.
		clientUser.Profile.Should().BeSameAs(profileAfterFirst);
	}

	[Fact]
	public async Task Changed_claims_within_the_window_republish_in_full() {
		var services = NewServices();
		var factory = new TestFactory(services);
		var clientUser = services.GetRequiredService<ClientUser>();

		await factory.CreateUserAsync(
			AccountWith(
				("sub", """ "user-1" """),
				("name", """ "Jane Smith" """),
				("customRoles", """ ["subscriber"] """)),
			Options());
		var profileAfterFirst = clientUser.Profile;

		// Same user, same window — but the mint now carries an extra role. The content-aware
		// key must publish (atomically: principal + profile + notification), not deduplicate.
		var second = await factory.CreateUserAsync(
			AccountWith(
				("sub", """ "user-1" """),
				("name", """ "Jane Smith" """),
				("customRoles", """ ["subscriber","admin"] """)),
			Options());

		clientUser.Principal.Should().BeSameAs(second);
		clientUser.Profile.Should().NotBeSameAs(profileAfterFirst);
		new ClaimsPrincipal(clientUser.Principal.Identity!).IsInRole("admin").Should().BeTrue();
	}

	[Fact]
	public async Task A_failed_transform_publishes_fail_open_and_identical_duplicates_dedupe() {
		var services = NewServices();
		var factory = new TestFactory(services, throwInMapIdentity: true);
		var clientUser = services.GetRequiredService<ClientUser>();
		var account = AccountWith(
			("sub", """ "user-1" """),
			("name", """ "Jane Smith" """));

		var first = await factory.CreateUserAsync(account, Options());
		var profileAfterFirst = clientUser.Profile;

		await factory.CreateUserAsync(account, Options());

		// Fail-open: the partially transformed principal is still published — and the
		// publication records exactly what was published, so a persistently failing transform
		// dedupes like any other identical content instead of re-publishing on every
		// duplicate call (transforms are re-attempted each call regardless of tracking).
		first.Identity!.IsAuthenticated.Should().BeTrue();
		clientUser.Principal.Should().BeSameAs(first);
		clientUser.Profile.Should().BeSameAs(profileAfterFirst);
	}

	[Fact]
	public async Task A_recovered_transform_republishes_via_the_changed_fingerprint() {
		var services = NewServices();
		var factory = new TestFactory(services, mappedRoles: ["admin"], throwInMapIdentity: true);
		var clientUser = services.GetRequiredService<ClientUser>();
		var account = AccountWith(
			("sub", """ "user-1" """),
			("name", """ "Jane Smith" """));

		await factory.CreateUserAsync(account, Options());
		var profileWhileFailing = clientUser.Profile;
		clientUser.Principal.IsInRole("admin").Should().BeFalse();

		// The transform starts succeeding: the mapped role changes the claim content, so the
		// fingerprint differs and the same user republishes inside the window — no waiting
		// for the window to expire.
		factory.ThrowInMapIdentity = false;
		var recovered = await factory.CreateUserAsync(account, Options());

		clientUser.Principal.Should().BeSameAs(recovered);
		clientUser.Profile.Should().NotBeSameAs(profileWhileFailing);
		clientUser.Principal.IsInRole("admin").Should().BeTrue();
	}

	[Fact]
	public async Task Claim_order_does_not_affect_the_fingerprint() {
		var services = NewServices();
		var factory = new TestFactory(services);
		var clientUser = services.GetRequiredService<ClientUser>();

		await factory.CreateUserAsync(
			AccountWith(("sub", """ "user-1" """), ("name", """ "Jane Smith" """)), Options());
		var profileAfterFirst = clientUser.Profile;

		// Same effective content, different member order — the sorted fingerprint must match
		// and the duplicate must dedupe.
		await factory.CreateUserAsync(
			AccountWith(("name", """ "Jane Smith" """), ("sub", """ "user-1" """)), Options());

		clientUser.Profile.Should().BeSameAs(profileAfterFirst);
	}

	[Fact]
	public async Task A_different_user_publishes_immediately() {
		var services = NewServices();
		var factory = new TestFactory(services);
		var clientUser = services.GetRequiredService<ClientUser>();

		await factory.CreateUserAsync(
			AccountWith(("sub", """ "user-1" """), ("name", """ "Jane" """)), Options());
		var profileForFirstUser = clientUser.Profile;

		var second = await factory.CreateUserAsync(
			AccountWith(("sub", """ "user-2" """), ("name", """ "Sam" """)), Options());

		// The window keys on the user — a different user must not be deduplicated.
		clientUser.Profile.Should().NotBeSameAs(profileForFirstUser);
		clientUser.Principal.Should().BeSameAs(second);
	}
}
