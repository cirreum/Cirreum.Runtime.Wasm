# Cirreum.Runtime.Wasm 4.0.0 — the connection credential names its audience

## Why this release exists

3.0.0 gave outbound remote connections the browser session's access token with no application code.
It asked for the wrong token.

The source called `RequestAccessToken()` with no options, so MSAL returned a token for the host's
default scopes — which `Cirreum.Runtime.Wasm.Msal` sets to `openid`, `profile`, `email`,
`offline_access` and `User.Read`. Every one of those is a Microsoft Graph scope, and a
Graph-audience token is rejected by a first-party API. The framework's own default therefore could
not authenticate a hub on the application's own backend, which is plausibly the most common thing
this feature exists for.

It failed the way this whole track kept failing: a token *was* issued, the connection *was*
attempted, and the rejection arrived at the server — reading as an authentication problem in the
application rather than as a credential aimed at the wrong resource. The first team to build on it
worked that out from source.

The obvious repair does not work either. Adding the API scope to the host's defaults makes a single
token span two resources, which Entra rejects rather than degrading.

## What changed

**A connection names its audience, and gets a token for it:**

```csharp
builder.AddRemoteConnection<ChatConnection>(options => {
    options.EndpointUri = new Uri("https://api.contoso.com/hubs/chat");
    options.Scopes = ["api://contoso/access_as_user"];
});
```

**A connection that declares no scopes receives no credential**, and the source logs a warning
naming the connection and endpoint. The connect fails with a stated cause instead of presenting a
token nobody asked for.

That is the substance of the release: making the failure visible is the fix. A wrong-audience
credential that reaches the server is indistinguishable from an application bug; a connection that
refuses to open, naming itself and its endpoint, is a five-second diagnosis.

**`AccessTokenRemoteConnectionTokenSource` is now `SessionRemoteConnectionCredentialSource`**,
implementing `IRemoteConnectionCredentialSource` from `Cirreum.Contracts` 5.0.0. The type is
internal; the rename reaches an application only if it registered a source of its own — and an
application that wrote one *because* the default did not work can now delete it and declare `Scopes`.

## What didn't change

Interaction is still never raised from a handshake. A token that would need a redirect yields no
credential and a warning, because a connection resolving a credential is mid-handshake, often on a
background reconnect, and a redirect from there navigates the application away from whatever the
user was doing.

Everything outside the connection credential — the SWA/CSP build feature, state, components, the
initialization orchestrator — is untouched.

## Upgrading

**Add `Scopes` to every connection's options before upgrading.** See
[MIGRATION-v4.md](MIGRATION-v4.md) for the deploy order and the before/after table.

## See also

- `Cirreum.Contracts` 5.0.0 — the credential contract, and why the source is told which connection
  it is supplying for.
- `Cirreum.RemoteConnections.SignalR` / `.WebSockets` 2.0.0 — the transports that carry the
  credential this runtime supplies.
