# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is **Cirreum.Runtime.Wasm**, a Blazor WebAssembly runtime client library that provides comprehensive client-side infrastructure for .NET applications. The project is built on .NET 10.0 and follows the Cirreum foundation framework architecture.

## Architecture

### Core Structure
- **src/Cirreum.Runtime.Wasm/**: Main library containing runtime client components
- **samples/Cirreum.Demo.Client/**: Demo Blazor WebAssembly application showcasing the library
- **build/**: MSBuild configuration files for packaging and CI/CD

### Key Architectural Patterns
- **Domain-Driven Design**: Built around `DomainApplication` and `DomainApplicationBuilder` patterns
- **State Management Pattern**: ViewModels with multiple persistence backends (Memory, Session, Local Storage, Container)
- **Component Inheritance Hierarchy**: Base classes for pages and components with state management
- **Dependency Injection**: Heavy use of DI with service registration extensions via `AddCirreumRuntimeClient()`
- **Builder Pattern**: Configuration through fluent APIs for domain applications
- **Notification Scoping**: Coalescing multiple state changes into single notifications

### Key Components
- **Authentication**: Custom authentication services with claims processing and no-auth fallback
- **Components**: Reusable Blazor components including authorization, presence, profile, theme management, and validation
- **State Management**: ViewModels and state components for local, memory, session, and container state
- **Security**: Client user management and authorization infrastructure
- **Startup Tasks**: Application initialization and configuration

### Technology Stack
- Blazor WebAssembly with .NET 10.0
- SCSS compilation via AspNetCore.SassCompiler
- Component architecture based on Cirreum framework packages
- Global usings for simplified namespace management

## Development Commands

### Build
```bash
dotnet build
```

### Run Demo Application
```bash
dotnet run --project samples/Cirreum.Demo.Client/Cirreum.Demo.Client.csproj
```
or from the demo directory:
```bash
cd samples/Cirreum.Demo.Client
dotnet run
```

### Testing
Check the demo application for test patterns - the library itself doesn't include unit tests in the repository.

### SCSS Compilation
SCSS files are automatically compiled via AspNetCore.SassCompiler during build. Configuration is in `sasscompiler.json` with different compression settings for Debug vs Release builds.

## Configuration

### Global Settings
- **.editorconfig**: Comprehensive C# coding standards with tab indentation and specific naming conventions
- **global.json**: .NET 10.0.100 SDK with latest feature rollforward
- **Directory.Build.props**: CI/CD detection, versioning, and package configuration
- **sasscompiler.json**: SCSS compilation settings with source map generation

### Package References
The library depends on:
- Microsoft.AspNetCore.Components.WebAssembly
- Cirreum.Components.WebAssembly
- Cirreum.Services.Wasm
- AspNetCore.SassCompiler
- Microsoft.Graph (for presence features)
- FluentValidation (for validation components)

### CI/CD Configuration
The build system automatically detects:
- Azure DevOps (TF_BUILD)
- GitHub Actions (GITHUB_ACTIONS)  
- Generic CI (CI)
- Local development with special versioning for Release builds

## Development Notes

### Namespace Convention
- Root namespace is `Cirreum.Runtime` (not matching folder structure by design)
- Extensive global usings reduce boilerplate in component files
- Follow .editorconfig rules for consistent code formatting

### State Management Pattern
The library implements a layered state management system:
- **ViewModels**: Abstract state containers with property change tracking
- **State Components**: Blazor components that manage specific state types
- **Page Base Classes**: Common functionality for different state scopes
- **Property Registration**: Explicit configuration requirements for state properties
- **Nested ViewModels**: Support for complex object hierarchies
- **EditContext Integration**: For Blazor form validation

### Component Architecture
- Components follow Blazor best practices with code-behind files
- SCSS styling with automatic compilation and source maps
- Presence and profile components for user interaction
- Theme management with dark/light mode support
- JavaScript interop module system for browser integration
- Session activity monitoring with expiration dialogs

### Authentication Patterns
The demo showcases multiple authentication options (currently commented out):
- Microsoft Entra (formerly Azure AD)
- Microsoft Entra External
- Generic OIDC providers
- No-auth fallback for development

### Key Implementation Details
- Use `DomainApplication.CreateCurrent<T>()` for accessing the current application instance
- State ViewModels require explicit property registration before use
- Components inherit from base classes like `MemoryStateComponent<T>` for state management
- SCSS files compile to CSS with the same name in the same directory
- Theme switching persists to local storage automatically