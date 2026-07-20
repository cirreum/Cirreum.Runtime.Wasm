# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is **Cirreum.Runtime.Wasm**, the Blazor WebAssembly runtime client for Cirreum
applications (Runtime layer). It provides the client-side `DomainApplication`
bootstrap, remote-service clients, authentication plumbing, state/ViewModel
infrastructure, and reusable runtime components, built on .NET 10.0.

## Architecture

### Core Structure
- **src/Cirreum.Runtime.Wasm/**: the runtime client library
  (`Authentication/`, `Components/`, `Extensions/`, `Security/`, `StartupTasks/`,
  `State/`, `SystemInitializers/`, `wwwroot/`)
- **samples/Cirreum.Demo.Client/**: demo Blazor WebAssembly application
- **build/**: MSBuild configuration, including `build/swa/` static-web-app assets
  (CSP policy generation)

### Key Architectural Patterns
- **Domain bootstrap**: `DomainApplication.CreateBuilder(args)` →
  `DomainApplicationBuilder` (implements `IClientDomainApplicationBuilder`) →
  `await builder.BuildAndRunAsync()` / `BuildAndRunAsync<TDomainType>()`
- **State Management Pattern**: ViewModels with multiple persistence backends
  (Memory, Session, Local Storage, Container), building on `Cirreum.Services.Wasm`
- **Component Inheritance Hierarchy**: base classes for pages and components with
  state management
- **Dependency Injection**: registration via `HostingExtensions` verbs — this repo
  ships `AddRemoteClient()`, `AddApplicationUserResolver()`,
  `AddDefaultAuthorization()`; `AddClientState()` / `AddSessionMonitoring()` come
  from `Cirreum.Services.Wasm`
- **Notification Scoping**: coalescing multiple state changes into single
  notifications (see the `Cirreum.Services.Wasm` CLAUDE.md for the rules)

### Authentication

This package ships the common client-side pieces: `CommonClaimsPrincipalFactory`,
claims processing, and the no-auth fallback. **IdP-specific composition lives in the
Runtime Extensions packages** — `Cirreum.Runtime.Wasm.Msal` (Entra/MSAL, ships
`MsalClaimsPrincipalFactory`) and `Cirreum.Runtime.Wasm.Oidc` (generic OIDC — e.g.
Descope — ships `OidcClaimsPrincipalFactory`). Apps install one of those and compose
auth through its verbs; this repo has no `AddEntraAuth`-style entry points of its own.

### Technology Stack
- Blazor WebAssembly with .NET 10.0
- SCSS compilation via AspNetCore.SassCompiler; JS minification via NUglify
- JavaScript interop module system for browser integration

## Development Commands

### Build
```bash
dotnet build
```

### Samples solution (local components)
`samples/Cirreum.Runtime.Wasm.Samples.slnx` builds the library chain from **local
project references** — `src/Directory.Build.props` flips `UseLocalComponents=true`
when the solution name is `Cirreum.Runtime.Wasm.Samples`, switching the
`Cirreum.Domain` / `Cirreum.Components.WebAssembly` / `Cirreum.Services.Wasm`
package references to project references. The referenced local repos live at
`Core/Cirreum.Domain`, `Core/Cirreum.Components.WebAssembly`, and
`Infrastructure/Cirreum.Services.Wasm`.

> The demo app (`Cirreum.Demo.Client`) currently targets pre-reset
> authorization/introspection APIs and does not compile — the library chain builds;
> the demo is pending a rehab against the current surface.

### Testing
The library has no test project yet.

### SCSS Compilation
SCSS files are compiled via AspNetCore.SassCompiler during build; configuration in
`sasscompiler.json` with different compression settings for Debug vs Release.

## Configuration

### Package References
- `Cirreum.Domain` (direct)
- `Cirreum.Components.WebAssembly` and `Cirreum.Services.Wasm`
  (package references; project references under `UseLocalComponents`)
- `Microsoft.AspNetCore.Components.WebAssembly`
- `AspNetCore.SassCompiler`, `NUglify` (build-time)

### CI/CD Configuration
The build system detects Azure DevOps (TF_BUILD), GitHub Actions (GITHUB_ACTIONS),
and generic CI (CI); CI derives the package version from the release tag, local
Release builds use the `1.0.100-rc` convention.

## Development Notes

### Namespace Convention
- Root namespace is `Cirreum.Runtime` (not matching folder structure by design)
- Extensive global usings reduce boilerplate in component files
- Follow `.editorconfig` (tabs, K&R) for consistent formatting

### State Management Pattern
- **ViewModels**: abstract state containers with property change tracking; state
  ViewModels require explicit property registration before use
- **State Components**: components inherit from non-generic base classes like
  `MemoryStateComponent`, `SessionStateComponent`, `LocalStateComponent`
- **Nested ViewModels** and **EditContext integration** for Blazor form validation

### Component Architecture
- Components follow Blazor best practices with code-behind files
- SCSS styling with automatic compilation; theme switching persists to local storage
- Presence and profile components; session activity monitoring with expiration dialogs

### DomainApplication Usage Example
```csharp
var builder = DomainApplication.CreateBuilder(args);

builder.RootComponents.Add<HeadOutlet>("head::after");
builder.RootComponents.Add<App>("#app");

// Remote services
builder.AddRemoteClient<MyApiClient>(options => {
    options.ServiceUri = new Uri("https://api.example.com/");
});

// Authentication — compose via a Runtime Extensions package:
//   Cirreum.Runtime.Wasm.Msal (Entra) or Cirreum.Runtime.Wasm.Oidc (OIDC IdPs)

// Client state (from Cirreum.Services.Wasm)
builder.AddClientState(state => state
    .RegisterState<IMyState, MyState>()
    .RegisterRemoteState<IMyRemoteState, MyRemoteState>()
);

await builder.BuildAndRunAsync();
```
