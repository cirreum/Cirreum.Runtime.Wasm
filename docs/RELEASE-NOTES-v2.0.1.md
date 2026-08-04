# Cirreum.Runtime.Wasm 2.0.1 — the router now sees the bootstrap client

## Why this release exists

2.0.0 replaced the client-side application-user resolver with the framework bootstrap client —
but `AppRouteView` still probed DI for the *removed* `IApplicationUserResolver` to decide
whether the application-user states apply. Under the new registration the probe always
answered no, so `NotProvisioned` and `Disabled` were unreachable and the router went straight
to `Ready` with a null application user: the exact outcome the major exists to fix, surviving
one layer above the fix. Same-day, pre-adoption.

## What changed

- **`AppRouteView` probes for the framework bootstrap client** (via
  `IServiceProviderIsService`, so no client — or `HttpClient` — is constructed just to answer
  a presence question). With `AddApplicationUser<TUser>` registered, the state machine
  evaluates the application-user states again: `204` → `NotProvisioned`,
  `IsEnabled = false` → `Disabled`, otherwise `Ready`. A registration test suite now guards
  the seam both components rely on.
- **The named remote-client registry is per service collection** (was process-static). A
  second container in the same process — a test host, a second builder — previously found
  names claimed by the first and silently skipped its own registrations. Within one
  application's composition, dedupe and the conflicting-options refusal are unchanged.

## Compatibility

Pure fixes; no surface change. Apps that took 2.0.0 should take 2.0.1 immediately — without
it, the disabled/not-provisioned experiences the major promises do not render.

## See also

- `docs/RELEASE-NOTES-v2.0.0.md` / `docs/MIGRATION-v2.md` — the major this corrects
