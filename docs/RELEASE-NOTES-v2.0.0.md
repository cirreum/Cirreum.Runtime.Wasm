# Cirreum.Runtime.Wasm 2.0.0 — the bootstrap stops depending on the pipeline it bootstraps

## Why this release exists

`AppRouteView` has always had a `ViewState.Disabled` branch, and it has never rendered for a
real disabled user. The record it needs arrived through an app-written client-side
`IApplicationUserResolver` calling the app's own authorizable operations — which the disabled
gate correctly denied, so the client never received the record whose `IsEnabled` it was about
to read. The orchestrator normalized the failure to a null user, and a disabled subscriber was
told they were never provisioned, with an error toast on top.

Every layer was individually defensible; the defect was architectural: **the client's
bootstrap path depended on the authorization pipeline it was bootstrapping.** v2 removes the
dependency instead of punching a hole in the gate.

## What's new

**`AddApplicationUser<TUser>(Uri serviceUri)`** replaces `AddApplicationUserResolver` — the
app supplies only what is genuinely its own, the concrete user type and the service URI:

```csharp
builder.AddAuthentication(auth => {
	auth.AddApplicationUser<User>(primaryApi);
});
```

The framework fetches the caller's own record from `GET /_cirreum/application-user` (mapped
automatically by `Cirreum.Runtime.Server` 1.2.0, shared route constant in `Cirreum.Domain` so
the two ends cannot drift). The endpoint requires authentication and nothing else, so the
record arrives even for a disabled caller — `ViewState.Disabled` renders for the first time —
and its `204` maps to a null user, keeping "no record" distinct. The call rides the standard
remote-service pipeline: default `AuthorizationMessageHandler`, the app's default token
scopes, `RemoteClient` telemetry.

Apps delete their client-side resolver, its caching apparatus, and any endpoint that existed
only to serve it.

**Build-time deferred-log validation.** The WASM builder now fails `Build` when
configuration-time checks wrote Warning-or-worse deferred entries. The WASM host has no
deferred-log flusher, so errors like Domain's dead-operations check — operations that will be
denied on every dispatch — were previously dropped silently and the app started looking
healthy. Same check the server builder has always run.

## Compatibility

Breaking: the two `AddApplicationUserResolver` overloads are removed (and the `Msal` / `Oidc`
wrapper verbs with them, in their own 2.0.0 releases) — deliberately a compile error rather
than a documentation note. The server-side registration surface is unchanged, and because the
app's own type still crosses the wire, `IUserState.GetApplicationUser<TUser>()` and every
component read keep working untouched. See `MIGRATION-v2.md` for the walkthrough.

## See also

- `docs/MIGRATION-v2.md` — find/replace table and walkthrough
- `Cirreum.Runtime.Server` 1.2.0 release notes — the endpoint's contract
- `docs/CHANGELOG.md` — the enumerated changes
