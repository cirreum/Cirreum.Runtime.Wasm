# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
