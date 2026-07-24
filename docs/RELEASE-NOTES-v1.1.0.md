# Cirreum.Runtime.Wasm 1.1.0 — client canonicalization of provisioned claims

## Why this release exists

The Cirreum Identity family now lets a server-side provisioner mint an application's own claims
into the issued token under a `custom*` namespace (`customRoles`, `customName`, `customTenant`, …),
collision-safe against native identity-provider claims. This release is the client counterpart:
it aliases those `custom*` claims back to the native names the rest of the client already reads.

## What's new

`CommonClaimsPrincipalFactory` gained a built-in canonicalization phase that runs during
`CreateUserAsync`, before the `ClaimsPrincipal` is built and before any app `IClaimsExtender`:

```csharp
this.MapIdentity(identity, account);
CustomClaimCanonicalizer.Canonicalize(identity);   // customRoles -> roles, customName -> name, …
this.ExtendClaims(identity, account);
```

It does two things, both universal boilerplate every app would otherwise re-implement:

- **Alias** each `custom*` claim to its native name. A minted claim is `custom` + an upper-cased
  first character, so `customRoles`/`customName` match while an unrelated `customer` claim is left
  untouched. The two named claims follow the identity's **configured** claim types — `customRoles`
  → `RoleClaimType`, `customName` → `NameClaimType` (the `options.RoleClaim`/`options.NameClaim` the
  provider set) — so `IsInRole` and `Identity.Name` resolve even when an OIDC app overrode them via
  `AddOidcAuth(roleClaimType:, nameClaimType:)`. Every other claim uses its derived name
  (`customTenant` → `tenant`).
- **Split** a `custom*` array claim — which arrives as a single JSON-array string
  (`["admin","user"]`) — into individual claims, as `IsInRole` requires. Provisioned arrays are
  contractually non-blank string arrays, so null, empty, and non-string entries are dropped (a
  boolean or object never becomes a role), and an empty scalar mints nothing. Each alias preserves
  the source claim's issuer, original issuer, and properties, with its value type pinned to String.

Because the factory is inherited by both `MsalClaimsPrincipalFactory` (Entra) and
`OidcClaimsPrincipalFactory` (Descope / generic OIDC), both providers get it with no wiring.

## Additive by design — the app owns precedence

The phase is **purely additive and idempotent; it never removes a claim and never adds an exact
`(type, value)` duplicate** (re-running it — or aliasing onto a claim type the identity already
carries — is safe). When both a native claim and its minted `custom*` counterpart are present,
both survive. The framework can't know whether the native
value is stale-and-to-be-replaced or complementary, so it doesn't guess — resolving that precedence
is the application's decision, made in its own `IClaimsExtender`, which runs *after* canonicalization
with full visibility of every claim (the original `custom*`, the native one, and the alias). The
framework owns the boilerplate; the app owns the judgment.

It is **inert when unneeded**: an app that canonicalizes at the IdP (Azure token config, a Descope
flow step) ships a token already in native form, so the phase finds no `custom*` claims and does
nothing. Always safe to run, never a dependency.

## Also fixed in this release

An adversarial review of `CommonClaimsPrincipalFactory` alongside this work surfaced two shipped
defects, both fixed here:

- **The deduplication window returned unprocessed principals.** A repeated `CreateUserAsync` for
  the same user within 30 seconds skipped identity mapping and claims extension but still handed
  the resulting principal to Blazor as the authentication state — on MSAL that meant a principal
  with no role claims at all until the next call outside the window, diverging from `IUserState`.
  The claim transforms now always run; the window deduplicates only state publication, and
  `ClientUser` mutation and subscriber notification form one logical publication, always
  performed together. The deduplication key is the user id plus an order-insensitive fingerprint
  of the processed claims (including the identity's name/role claim-type configuration): an
  identical duplicate call (the login double-fire) skips re-publication, while any content
  change — refreshed roles, different extender output, a recovered transform — publishes in
  full even within the window. Tracking records exactly what was published, so a persistent
  optional-transform failure dedupes instead of re-publishing on every duplicate call.
- **JSON `null` account members produced empty-string claims** (a dead null-check inherited from
  the upstream factory template). They are now skipped.

The account-claim conversion was also tightened: JSON string members map to unquoted claim
values, JSON `null`/`undefined` are skipped, arrays keep their raw JSON (what the canonicalizer
splits), and the fallback path is reflection-free for trimmed/AOT WASM publishing.

## Compatibility

- No API changes. `IClaimsExtender` and `AddClaimsExtender<T>()` are unchanged; app extenders now
  simply run against already-canonicalized claims.
- Pairs with `Cirreum.IdentityProvider 2.0.0`, `Cirreum.Identity.EntraExternalId 2.1.0`, and
  `Cirreum.Identity.Oidc 1.1.0`. No coordinated client change is required beyond consuming this
  version — the two provider packages inherit the behavior by re-pin.

## See also

- `Cirreum.IdentityProvider 2.0.0` — the server-side provisioning mint this canonicalizes.
- `Cirreum.Identity.EntraExternalId 2.1.0` / `Cirreum.Identity.Oidc 1.1.0` — the wire projections.
