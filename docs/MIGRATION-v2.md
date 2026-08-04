# Migration Guide: Cirreum.Runtime.Wasm 1.x → 2.0

## Why v2

A WebAssembly client could never tell a user they were disabled, because reading the record
that says so required not being disabled. The client resolved its application user through an
app-written `IApplicationUserResolver` that called the app's own authorizable operations —
which pass through the authorization pipeline, whose disabled gate denied them. The failure
was silent and landed on the wrong screen: the initialization orchestrator normalized the
denial to a null application user, and the router showed the "not provisioned" experience
(with an error toast) to a disabled subscriber.

The architectural rule v2 enforces: **a bootstrap path must not depend on the system it
bootstraps.** The server has always had this property — `UserStateAccessor` resolves the
application user outside the dispatcher. v2 gives the client the same property: the framework
fetches the caller's own record from a framework-owned endpoint
(`GET /_cirreum/application-user`, mapped automatically by `Cirreum.Runtime.Server` 1.2.0)
that requires authentication and nothing else. No authorization gate stands between a
disabled caller and the record describing their state — and no exemption is introduced
anywhere; the disabled gate stays absolute for operations.

Removing the old verb (rather than obsoleting it) makes the migration a compile error rather
than a documentation note.

## Breaking Changes — Find/Replace Table

| v1.x | v2.0 |
|------|------|
| `builder.AddApplicationUserResolver<MyResolver>()` | `builder.AddApplicationUser<MyUser>(serviceUri)` |
| `builder.AddApplicationUserResolver(sp => new MyResolver(...))` | `builder.AddApplicationUser<MyUser>(serviceUri)` |
| An app-written client-side `IApplicationUserResolver` implementation | Delete it — and its caching apparatus, and any API endpoint that existed only to serve it |

`MyUser` is your `IApplicationUser` implementation — the same type your **server-side**
resolver (`CirreumAuthenticationBuilder.AddApplicationUserResolver<T>()`, unchanged) returns.
`serviceUri` is the base URI of your Cirreum server — the same URI your remote clients
already use.

## New Capabilities

- **`ViewState.Disabled` renders for the first time.** A disabled caller receives their
  record (the bootstrap endpoint is authentication-only), `IsEnabled` is finally readable,
  and the router takes the branch that has been waiting since it was written.
- **"No record" and "no answer" are distinct.** The endpoint returns `204` for a caller with
  no application-user record; the client maps it to a null application user
  (`ViewState.NotProvisioned`), reserving errors for genuine failures.
- **Build-time deferred-log validation.** `Build` now fails when configuration-time checks
  wrote Warning-or-worse deferred entries (e.g. operations with no authorizer, which would be
  denied on every dispatch). These were previously dropped silently on WASM.

## Migration Walkthrough

1. **Update the package** (and `Cirreum.Runtime.Wasm.Msal` / `.Oidc` to 2.0.0 if you use
   their wrapper verbs — they forward to the same registration).
2. **Replace the registration.** Where you called `AddApplicationUserResolver<TResolver>()`,
   call `AddApplicationUser<TUser>(serviceUri)` with your user type and your API's base URI:

   ```csharp
   var primaryApi = new Uri("https://api.example.com/");

   builder.AddAuthentication(auth => {
       auth.AddApplicationUser<User>(primaryApi);
   });
   ```

3. **Delete your client-side resolver** — the class, any caching around it, and any endpoint
   your API exposed solely for it. The server-side resolver registration is unchanged and is
   what the bootstrap endpoint reads from.
4. **Verify the server is on `Cirreum.Runtime.Server` 1.2.0+**, which maps the bootstrap
   endpoint automatically when a server-side `IApplicationUserResolver` is registered.
5. **Check what your server resolver returns.** Its return type now crosses the wire to the
   browser — if your server entity carries fields not intended for a client, trim them or
   resolve a client-facing shape.

## What Didn't Change

- The **server-side** registration surface: apps keep calling
  `CirreumAuthenticationBuilder.AddApplicationUserResolver<T>()`.
- `IApplicationUser` / `IOwnedApplicationUser`, `IUserState.GetApplicationUser<TUser>()`, and
  every read your components make — the app's own type still crosses the wire, so typed
  access keeps working with no changes.
- The initialization phases, ordering, and `AppRouteView`'s routing branches.
- The disabled gate itself: absolute, no exemptions, exactly as ADR-0040 shipped it.

## Downstream Package Impact

| Package | Impact |
|---------|--------|
| `Cirreum.Runtime.Wasm.Msal` 2.0.0 | wrapper verbs replaced to match (4 removed, `AddApplicationUser` added) |
| `Cirreum.Runtime.Wasm.Oidc` 2.0.0 | same (2 removed) |
| `Cirreum.Runtime.Server` 1.2.0 | serves the bootstrap endpoint (additive) |
