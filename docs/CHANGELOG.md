# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.47] - 2026-07-08

### Updated

- Updated NuGet packages as part of the lower-layer changes.

## [1.0.46] - 2026-07-06

### Updated

- Updated NuGet packages.

## [1.0.45] - 2026-07-04

### Fixed

- **Dropped the `Cirreum.AuthenticationProvider` reference in favor of a direct `Cirreum.Domain` reference.** `IUserProfileEnrichmentBuilder`/`ClaimsUserProfileEnricher` (used by `HostingExtensions.ProfileEnrichment.cs`) relocated to `Cirreum.Contracts`/`Cirreum.Domain` (host-agnostic profile enrichment, not an Authentication-track concern). Referencing `Cirreum.Domain 1.2.0` directly (rather than relying transitively on `Cirreum.Services.Wasm`'s older pin) ensures the resolved version actually carries these types. No `using` changes needed — both types live in the root `Cirreum` namespace, already visible from `Cirreum.Runtime` without qualification.

## [1.0.44] - 2026-07-04

### Fixed

- **Added the missing explicit `Cirreum.AuthenticationProvider` reference.** This repo's own source (`HostingExtensions.ProfileEnrichment.cs`: `IUserProfileEnrichmentBuilder`, `ClaimsUserProfileEnricher`) has always used `Cirreum.AuthenticationProvider` types, but only ever compiled by riding transitively on legacy `Cirreum.Core` through an old `Cirreum.Services.Wasm` pin. Now that `Cirreum.Services.Wasm`/`Cirreum.Components.WebAssembly` have cut over to the foundation-reset packages (Core-free), this repo needs — and now has — its own direct reference.
- **`InitializationOrchestrator` no longer calls the removed `IUserState.Identity`.** That property was deliberately dropped from the reset's `UserStateBase`/`IUserState` in favor of casting `Principal.Identity` (documented on `IUserState.Principal`) — this repo's profile-enrichment call site hadn't been updated to the new pattern. Switched to `(ClaimsIdentity)clientUser.Principal.Identity!`.

### Updated

- Updated NuGet packages (`Cirreum.Components.WebAssembly` → 1.0.40, `Cirreum.Services.Wasm` → 1.0.27 — completes this repo's transitive Tier-2 foundation cutover; `AspNetCore.SassCompiler` → 1.101.0; `Microsoft.AspNetCore.Components.WebAssembly` → 10.0.9).

## [1.0.43] - 2026-05-10

### Updated

- Updated NuGet packages.

## [1.0.41] - 2026-05-01

Explicitly enforces the **single-IdP-client invariant** at registration
time — Cirreum WASM hosts bind to exactly one IdP, so only one
`IApplicationUserResolver` may be registered. Pairs with the
`Cirreum.Core 5.0.0` dependency bump.

Per-scheme `IApplicationUserResolver` dispatch is a server-side concern
where multi-IdP fan-in is the common case (see `Cirreum.Services.Server`
and `Cirreum.Runtime.Authorization` releases). On the client side it
would be vestigial complexity, so this release deliberately keeps WASM
resolution simple: one registration, one resolver, no dispatch.

### Fixed

- **`HostingExtensions.AddApplicationUserResolver` now fails fast on a
  second registration.** Previously `TryAddScoped` silently ignored a
  second call, leaving the first registration in effect — the kind of
  quiet bug that produces "I registered MyResolver, why is OldResolver
  firing?" debugging sessions. Both overloads (type and factory) now
  throw `InvalidOperationException` on a duplicate registration, with an
  error message naming the single-IdP-client invariant and pointing at
  the escape hatch (`o.UserOptions.AuthenticationType` + a composite
  resolver) for the rare host that genuinely needs multi-IdP dispatch on
  the client side. Hosts with exactly one registered resolver are
  unaffected.

### Updated

- **`Cirreum.Core`** — `4.0.2` → `5.0.1` (transitive major bump). Picks up
  `AuthenticationContextKeys` and `IApplicationUserResolver.Scheme`.

### Migration

**Existing single-resolver apps — no code change required.**

**Apps that previously called `AddApplicationUserResolver<T>()` twice
will now throw at startup.** Pre-5.0 the second call was silently ignored
(`TryAddScoped` semantics), so the first registration won and any
"replacement" attempt produced no error and no effect. Either remove the
duplicate registration, or — if you actually need to swap implementations
at startup based on configuration — pick the resolver type *before*
calling `AddApplicationUserResolver` rather than calling it twice.

**Resolver `Scheme` value (server hosts only):** Cirreum.Core 5.0 added
`IApplicationUserResolver.Scheme` to support per-scheme dispatch on
multi-IdP server hosts. WASM resolvers should leave it at the default
(`null`); the orchestrator no longer reads `Scheme` and the registration
extension allows only one resolver, so the property is unused on this
side.
