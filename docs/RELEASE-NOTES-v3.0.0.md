# Cirreum.Runtime.Wasm 3.0.0 — a policy by default, and a token for connections

## Why this release exists

The SWA build feature shipped a Content-Security-Policy only for apps that had created a
policy file. Every other app published with no CSP header at all — the least protected
outcome, reached by doing nothing, and never stated anywhere. A published Blazor app is
static files served from a CDN edge; the policy is the only thing standing between it and an
injected script.

The feature also carried five defects that shared one shape: they failed silently. A comment
at the top of a policy file deleted the directive beneath it. `upgrade-insecure-requests`
aborted generation outright. An unmatched `__CSP__` placeholder shipped as a literal header
value, exiting successfully. Environment overlays were skipped with no message under
`dotnet publish`. And on Windows the generator did not parse at all under the very shell the
build invokes. Each produced a build that reported success.

Alongside that, the connection track: `Cirreum.RemoteConnections.SignalR` and
`.WebSockets` reach the browser in this wave, and a browser connection needs the session's
credential.

## What's new

**A Content-Security-Policy applies to every published app.** The framework baseline no
longer waits for the app to opt in. App and environment policies merge on top exactly as
before.

**The baseline is hardened**, following Microsoft's published CSP guidance for client-side
Blazor: `base-uri 'self'` (which does not fall back to `default-src`, so it was previously
unrestricted), `object-src 'none'`, `img-src 'self' data:`, and `upgrade-insecure-requests`.
Microsoft's `connect-src 'none'` is deliberately not adopted — it would block every API call,
and Cirreum apps call APIs through `RemoteClient`. The baseline is now commented, saying why
`'wasm-unsafe-eval'` and `'unsafe-inline'` are present and what an app supplies itself.

**Five silent failures are fixed.** Comment lines no longer take the following directive with
them; valueless directives parse; the `__CSP__` placeholder is matched case-insensitively and
fails generation if it survives; overlay files that resolve to no environment warn instead of
vanishing; and the generator is ASCII with a BOM, so Windows PowerShell 5.1 parses it.

**`CirreumPwshExecutable`** makes the generator's shell overridable — force PowerShell 7 on
Windows, or give an absolute path on an agent where neither name is on `PATH`. It was
previously private and unconditional, so an app-supplied value was discarded.

**The SWA/CSP feature is documented in the README.** It shipped without any coverage.

**Outbound remote connections receive the session's access token.** An
`IRemoteConnectionTokenSource` backed by the host's `IAccessTokenProvider` registers wherever
the app authenticates, so a connection presents the signed-in user's credential with no
wiring. It returns no token when one cannot be obtained without user interaction — a
connection resolving a credential is mid-handshake, often on a background reconnect, and a
redirect from there would navigate the app away from what the user was doing. A no-auth app
registers nothing; an app supplying its own source keeps it.

## Upgrading

**Read [MIGRATION-v3.md](MIGRATION-v3.md) before deploying.** The break is in deployment
configuration, not application code: a 2.x app compiles unchanged, and the failure mode is a
browser console error after deployment rather than a build error. Nothing in the build can
detect that an app depends on a cross-origin resource.

In short: an app with no policy file that loads anything cross-origin must declare those
origins in `Properties\csp.policy`, or set `<CirreumGenerateCsp>false</CirreumGenerateCsp>` to
emit no policy. An app that already supplies a policy inherits the hardened baseline —
`object-src 'none'` and `upgrade-insecure-requests` are the two that can change behaviour.

## See also

- `Cirreum.RemoteConnections.SignalR` and `Cirreum.RemoteConnections.WebSockets` 1.0.0 — the
  connections this runtime supplies credentials to.
- `Cirreum.Runtime.Server` 1.4.0 — the server half, which accepts the query-carried credential
  a browser has no alternative to on a WebSocket upgrade.
