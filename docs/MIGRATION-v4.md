# Cirreum.Runtime.Wasm v3 → v4 Migration

v4 fixes the credential a Blazor WebAssembly application supplies to its outbound remote
connections. **The change is behavioural, and a connection that worked by accident in v3 may now
fail at connect — which is the point.**

If the application opens no remote connections, upgrading is a version bump.

---

## ⚠️ Declare each connection's audience before upgrading

In v3, the framework's credential source called `RequestAccessToken()` with no options. MSAL then
returns a token for the host's *default* scopes, which `Cirreum.Runtime.Wasm.Msal` sets to
`openid`, `profile`, `email`, `offline_access` and `User.Read` — every one of them a Microsoft Graph
scope.

A Graph-audience token is rejected by a first-party API. So the credential a connection received out
of the box could not authenticate a hub on the application's own backend, and the rejection arrived
at the server, surfacing as an authentication problem in the application rather than as a credential
aimed at the wrong resource.

Adding the API scope to the host's defaults does not work either: a single token cannot span two
resources, and mixing `User.Read` with an `api://` scope errors at Entra rather than degrading.

**In v4 a connection names its audience:**

```csharp
builder.AddRemoteConnection<ChatConnection>(options => {
    options.EndpointUri = new Uri("https://api.contoso.com/hubs/chat");
    options.Scopes = ["api://contoso/access_as_user"];
});
```

A connection that declares **no** scopes receives no credential, and the source logs a warning
naming the connection and endpoint. The connect then fails with a stated cause rather than
presenting a token for an audience nobody asked for.

| Connection | v3 | v4 |
| --- | --- | --- |
| declares `Scopes` | ignored — a default-scope token was sent | a token for those scopes |
| declares no `Scopes` | a default-scope token, usually Graph | no credential, and a warning |
| supplies its own `CredentialProvider` or `AuthorizationHeader` | unchanged | unchanged |

Deploy in this order:

1. Add `Scopes` to every connection's options, naming the audience it authenticates against.
2. Upgrade.

An application that genuinely wants the host's default scopes states them explicitly.

## The type renamed

`AccessTokenRemoteConnectionTokenSource` is now `SessionRemoteConnectionCredentialSource`,
implementing `IRemoteConnectionCredentialSource` from `Cirreum.Contracts` 5.0.0.

The type is internal, so this reaches an application only if it registered its own source. That
implements the new interface instead:

```csharp
// v3
public sealed class ApiTokenSource : IRemoteConnectionTokenSource {
    public ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default) { ... }
}

// v4
public sealed class ApiCredentialSource : IRemoteConnectionCredentialSource {
    public ValueTask<AuthorizationHeaderSettings?> GetCredentialAsync(
        RemoteConnectionTokenRequest request, CancellationToken ct = default) { ... }
}
```

`Cirreum.Contracts`' `MIGRATION-v5.md` carries the full before/after.

An application that wrote its own source *because* the framework default did not work can now delete
it and declare `Scopes` instead.

## What didn't change

- Interaction is still never raised from a handshake. A token that would need a redirect yields no
  credential and a warning, because a connection resolving a credential is mid-handshake, often on a
  background reconnect.
- The source is still registered only where the host authenticates, and still with `TryAdd`, so an
  application supplying its own keeps it.
- Everything else in the runtime: the SWA/CSP build feature, state, components, and the
  initialization orchestrator.
