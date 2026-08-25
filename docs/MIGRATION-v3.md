# Migrating to Cirreum.Runtime.Wasm 3.0

The 3.0 break is in **deployment configuration, not application code**. No API changed, and
no source edit is required — a 2.x app compiles unchanged against 3.0.

What changed is the `staticwebapp.config.json` generated during `dotnet publish` for Azure
Static Web Apps: a Content-Security-Policy is now emitted for **every** Blazor WebAssembly
app, where previously it was emitted only for apps that had supplied their own policy file.

**The failure mode is a browser console error after deployment, not a build error.** Nothing
in the build detects that an app depends on a cross-origin resource. Read this before
deploying 3.0, not after.

## Does this affect me?

| Situation | Action |
| --- | --- |
| Not deploying to Azure Static Web Apps | None. Nothing consumes the generated file. |
| `CirreumGenerateSwaConfig` is already `false` | None. No config is generated. |
| App has `Properties\csp.policy` today | Review the hardened baseline below. |
| App has no policy file | **Action required** — see below. |

## If your app has no policy file

Before 3.0 these apps published with no `Content-Security-Policy` header, so the browser
applied no restrictions. From 3.0 the framework baseline applies, and it permits
**same-origin resources only**.

Anything the app loads or calls from another origin is blocked unless declared:

- the API it calls, when the API is not served from the same origin as the app
- fonts, images, scripts or stylesheets from a CDN
- analytics, telemetry, error-reporting or support-widget endpoints
- an authentication provider's endpoints, when the sign-in flow uses `fetch` or a framed page

Declare them in `Properties\csp.policy`, using ordinary CSP syntax — one directive per line,
`#` or `//` for comments:

```
# API
connect-src 'self' https://api.myapp.com;

# CDN assets
img-src   https://cdn.myapp.com;
font-src  https://cdn.myapp.com;

# Identity provider
connect-src https://login.microsoftonline.com;
frame-src   https://login.microsoftonline.com;
```

The file **adds to** the baseline rather than replacing it, so there is no need to restate
framework defaults. Repeating a directive is fine: sources union.

### Finding what to declare

Publish, deploy to a non-production environment, open the app with the browser console
visible, and exercise sign-in and the main flows. Each blocked request reports the directive
that rejected it:

```
Refused to connect to 'https://api.myapp.com/orders' because it violates the
following Content Security Policy directive: "connect-src 'self'".
```

Add the origin under the named directive and repeat until the console is clean.

### If you need to ship before doing that work

```xml
<PropertyGroup>
  <CirreumGenerateCsp>false</CirreumGenerateCsp>
</PropertyGroup>
```

This emits the config with no `Content-Security-Policy` header — the 2.x behaviour for an app
with no policy file. Routes, navigation fallback and the other security headers are
unaffected. Treat it as temporary: it opts the app out of a protection it would otherwise
have.

## If your app already has a policy file

The baseline it merges with is hardened, following Microsoft's published CSP guidance for
client-side Blazor. Two additions can block something that previously worked:

- **`object-src 'none'`** — `<object>`, `<embed>` and `<applet>` are blocked. Previously
  these fell back to `default-src 'self'` and were permitted from the app's own origin. If
  the app embeds a PDF viewer or similar, add `object-src 'self'` (or the relevant origin) to
  `csp.policy`; per the CSP specification a source list containing `'none'` alongside other
  sources ignores the `'none'`.
- **`upgrade-insecure-requests`** — `http://` subresource URLs are rewritten to `https://`.
  A resource genuinely only available over `http://` will fail. The correct fix is to serve
  it over `https://`.

Also added, and unlikely to break anything: `base-uri 'self'` (previously unrestricted, as
`base-uri` does not fall back to `default-src`) and `img-src 'self' data:`, which is a
widening — `data:` images were previously blocked by the `default-src 'self'` fallback.

Microsoft's guidance also lists `connect-src 'none'`. Cirreum deliberately does not adopt it:
it would block every API call, and Cirreum apps call APIs through `RemoteClient`.

## Verifying before you deploy

The generated file is written to the publish output:

```bash
dotnet publish -c Release
cat bin/Release/net10.0/publish/wwwroot/staticwebapp.config.json
```

Check the `Content-Security-Policy` value in `globalHeaders` and confirm every origin the app
needs is present.

Environment overlays (`Properties\csp.{Env}.policy`) are selected by `EnvironmentName`, which
`dotnet publish` does **not** set on its own — pass `-p:EnvironmentName=Staging`. From 3.0 the
build warns when overlay files exist but no environment was resolved, so an overlay is no
longer skipped silently.

## Also in 3.0

The generator itself had several defects fixed — it failed to parse under Windows PowerShell
5.1, could leave the `__CSP__` placeholder in the deployed config, dropped a directive
following a comment line, and aborted on valueless directives such as
`upgrade-insecure-requests`. See the changelog for details. None of these require action.
