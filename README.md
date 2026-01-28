# Cirreum.Runtime.Wasm

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Runtime.Wasm.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Runtime.Wasm/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Runtime.Wasm.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Runtime.Wasm/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Runtime.Wasm?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Runtime.Wasm/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Runtime.Wasm?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Runtime.Wasm/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**Comprehensive client-side runtime infrastructure for Blazor WebAssembly applications**

## Overview

**Cirreum.Runtime.Wasm** provides a complete foundation for building robust Blazor WebAssembly applications with built-in support for authentication, state management, theming, user presence, and component infrastructure.

## Features

- **Authentication & Security**
  - Flexible authentication with claims processing
  - No-auth fallback for development scenarios
  - Post-processing hooks for user enrichment
  
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
    .AddDataStores()
        .WithAutoInitialization()
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

### Session Monitoring

```razor
<!-- Monitor user activity and session expiration -->
<SessionActivityMonitor WarningMinutes="5" />
```

## Architecture

The library is organized into several key namespaces:

- `Cirreum.Runtime.Authentication` - Authentication infrastructure and claims processing
- `Cirreum.Runtime.Components` - Reusable Blazor components
- `Cirreum.Runtime.Components.ViewModels` - State management and view model patterns
- `Cirreum.Runtime.Security` - Security providers and user management
- `Cirreum.Runtime.StartupTasks` - Application initialization tasks

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

Given its foundational role, major version bumps are rare and carefully considered.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*