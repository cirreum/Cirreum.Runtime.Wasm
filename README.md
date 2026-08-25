# Cirreum.Runtime.Wasm

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Runtime.Wasm.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Runtime.Wasm/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Runtime.Wasm.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Runtime.Wasm/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Runtime.Wasm?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Runtime.Wasm/releases)
[![License](https://img.shields.io/badge/license-MIT-F2F2F2?style=flat-square&labelColor=1F1F1F)](https://github.com/cirreum/Cirreum.Runtime.Wasm/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**Comprehensive client-side runtime infrastructure for Blazor WebAssembly applications**

## Overview

**Cirreum.Runtime.Wasm** provides a complete foundation for building robust Blazor WebAssembly applications with built-in support for authentication, state management, theming, user presence, and component infrastructure.

## Features

- **Authentication & Security**
  - Flexible authentication with claims processing and extension
  - `AppRouteView` — drop-in route view that gates on auth, initialization, and app user readiness
  - Initialization orchestrator for sequenced startup (auth, app user, enrichment, remote state)
  - No-auth fallback for development scenarios
  - Session access token supplied to outbound remote connections (`IRemoteConnectionTokenSource`)
  
- **State Management**
  - Multiple state scopes: Memory, Session, Local Storage, and Container
  - ViewModels with property change tracking
  - Persistent and transient state patterns
  
- **Component Library**
  - Session activity monitoring with expiration dialogs
  - User presence badges and indicators
  - Profile cards and management
  - Theme switcher with dark/light mode support
  - Validation components with FluentValidation integration
  
- **Deployment**
  - `staticwebapp.config.json` generated into the publish output for Azure Static Web Apps
  - Content-Security-Policy composed from a framework baseline, an app policy, and an optional per-environment overlay
  - Inline-script hashes computed from the published `index.html` (the Blazor importmap, plus any opt-in `<script id="...">`)

- **Developer Experience**
  - Comprehensive base classes for pages and components
  - Startup task infrastructure
  - JavaScript interop module system
  - SCSS compilation and theming support

## Installation

```bash
dotnet add package Cirreum.Runtime.Wasm
```

## Quick Start

```csharp
using Cirreum.Runtime;

var builder = DomainApplication.CreateBuilder(args);

// Configure root components
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.RootComponents.Add<App>("#app");

// Configure authentication (choose one)
builder.AddEntraAuth(tenantId, clientId);
// or builder.AddEntraExternalAuth(domain, clientId);

// Configure state management
builder.AddClientState(state => state
    .RegisterState<IMyState, MyState>()
    .RegisterRemoteState<IMyRemoteState, MyRemoteState>()
);

await builder.BuildAndRunAsync();
```

## Usage Examples

### State Management

```csharp
// Using a memory state component
@inherits MemoryStateComponent

@code {
    protected override void OnMemoryStateChanged()
    {
        // Handle state changes - StateHasChanged() is called automatically
    }
}
```

### User Presence

```razor
<!-- Display user presence badge -->
<UserPresenceBadge UserId="@userId" Size="PresenceBadgeSize.Large" />
```

### Theme Management

```razor
<!-- Theme selector component -->
<ThemeSelector />
```

### AppRouteView

```razor
<!-- Drop-in replacement for RouteView/AuthorizeRouteView -->
<!-- Handles auth gating, initialization, app user checks, and layout switching -->
<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <AppRouteView RouteData="@routeData"
                      DefaultLayout="@typeof(MainLayout)"
                      PendingLayout="@typeof(SplashLayout)">
            <NotAuthorized>
                <RedirectToUnauthorized UnauthorizedPage="/unauthorized" />
            </NotAuthorized>
        </AppRouteView>
    </Found>
</Router>
```
  
### Session Monitoring

```razor
<!-- Monitor user activity and session expiration -->
<SessionActivityMonitor WarningMinutes="5" />
```

### Azure Static Web Apps configuration

On `dotnet publish` of a Blazor WebAssembly project, the package generates
`staticwebapp.config.json` into the publish output. The source `wwwroot` is left untouched.

**A Content-Security-Policy is applied by default.** The framework baseline covers what a
Blazor WebAssembly app needs to boot and run from its own origin — `'wasm-unsafe-eval'` for
the Mono runtime, `'unsafe-inline'` on `style-src` for inline style attributes, `data:`
images, `base-uri`/`object-src`/`frame-ancestors` locked down, and
`upgrade-insecure-requests`.

The baseline permits **same-origin resources only**, which means an app that calls an API on
another origin, loads fonts or images from a CDN, or talks to any third-party endpoint must
declare those itself. Add a policy file to the project's `Properties` folder:

```
Properties/csp.policy              app policy - your API, CDNs, third-party origins
Properties/csp.Staging.policy      optional per-environment overlay
```

```
connect-src 'self' https://api.myapp.com;
img-src https://cdn.myapp.com;
```

Files use the same syntax as a CSP header — `directive source source;` per line, with `#` or
`//` comment lines. Directives merge baseline → app policy → environment overlay, and source
lists are **unioned**: an app policy adds sources to a directive rather than replacing them,
so the baseline's entries cannot be removed by an app policy.

To emit no Content-Security-Policy header at all — the rest of the config, including routes
and other security headers, is unaffected:

```xml
<PropertyGroup>
  <CirreumGenerateCsp>false</CirreumGenerateCsp>
</PropertyGroup>
```

Prefer widening the policy in `csp.policy` over disabling it.

The environment overlay is selected by `EnvironmentName`, which **`dotnet publish` does not
set on its own**:

```bash
dotnet publish -p:EnvironmentName=Staging
```

Use `CspEnvironmentName` instead to decouple the overlay from the ASP.NET environment. If
overlay files are present but no environment is resolved, or an environment is resolved but
its file is missing, the build warns rather than silently skipping the overlay. Overlay file
names are matched case-sensitively on Linux build agents.

Inline scripts are hashed from the published `index.html`: the Blazor importmap always, plus
any elements named in `CspInlineScriptIds`.

```xml
<PropertyGroup>
  <CspInlineScriptIds>app-boot,analytics-init</CspInlineScriptIds>
</PropertyGroup>
```

Supply `wwwroot/staticwebapp.config.template.json` to replace the built-in template; the
`__CSP__` placeholder marks where the generated policy is substituted, and the whole entry is
removed when no policy applies.

| Property | Purpose |
| --- | --- |
| `CirreumGenerateSwaConfig` | Set `false` to skip generating the config entirely |
| `CirreumGenerateCsp` | Set `false` to emit the config with no Content-Security-Policy header |
| `CspEnvironmentName` | Overlay selector, independent of `EnvironmentName` |
| `CspInlineScriptIds` | Additional inline `<script id="...">` elements to hash |
| `SwaConfigTemplate` / `CspBasePolicy` / `CspAppPolicy` / `CspEnvPolicy` | Override the conventional paths |
| `CirreumPwshExecutable` | PowerShell used to run the generator (`powershell` on Windows, `pwsh` elsewhere) |

## Architecture

The library is organized into several key namespaces:

- `Cirreum.Runtime.Authentication` - Authentication infrastructure and claims processing
- `Cirreum.Runtime.Components` - Reusable Blazor components including `AppRouteView`
- `Cirreum.Runtime.Components.ViewModels` - State management and view model patterns
- `Cirreum.Runtime.Initialization` - Initialization orchestrator and state tracking
- `Cirreum.Runtime.Security` - Security providers and user management

## Demo Application

The repository includes a comprehensive demo application showcasing all features:

```bash
cd samples/Cirreum.Demo.Client
dotnet run
```

The demo includes examples of:
- Navigation layouts with theming
- User presence indicators
- State management patterns
- Time zone and internationalization features
- Weather data with session state
- Notification system

## Contribution Guidelines

1. **Be conservative with new abstractions**  
   The API surface must remain stable and meaningful.

2. **Limit dependency expansion**  
   Only add foundational, version-stable dependencies.

3. **Favor additive, non-breaking changes**  
   Breaking changes ripple through the entire ecosystem.

4. **Include thorough unit tests**  
   All primitives and patterns should be independently testable.

5. **Document architectural decisions**  
   Context and reasoning should be clear for future maintainers.

6. **Follow .NET conventions**  
   Use established patterns from Microsoft.Extensions.* libraries.

## Versioning

Cirreum.Runtime.Wasm follows [Semantic Versioning](https://semver.org/):

- **Major** - Breaking API changes
- **Minor** - New features, backward compatible
- **Patch** - Bug fixes, backward compatible

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*