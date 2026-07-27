# Cirreum.Runtime.Wasm 1.2.0 — onto the Kernel 2.0.0 foundation

## Why this release exists

`Cirreum.Contracts` 2.0.0 renamed `IPageState` to `IBrowserDocumentState` and renamed its
`PageTitle*` members to `DocumentTitle*`. This is the Blazor client following that rename, plus the
re-pin onto the released foundation:

| Package | Before | After |
|---|---|---|
| `Cirreum.Domain` | 1.3.1 | **2.0.0** |
| `Cirreum.Services.Wasm` | 1.0.35 | **1.1.0** |
| `AspNetCore.SassCompiler` | 1.101.0 | 1.102.0 |

## Does this break you?

**Not from this package.** Nothing in its public surface changed:

- `AppPageTitle`'s only public member is its `ChildContent` parameter, untouched.
  `<AppPageTitle>…</AppPageTitle>` renders exactly as before — the component now injects
  `IBrowserDocumentState` instead of `IPageState`, but that is its own plumbing.
- `ConfigurePageState` became `ConfigureDefaultDocumentState`, and it is `sealed class` with no
  access modifier — **internal**. Apps never named it.

**You will hit a break, but it belongs upstream.** If your own code injects `IPageState` or reads
`PageTitlePrefix` / `PageTitleSeparator` / `PageTitleSuffix`, that type is public and lives in
`Cirreum.Contracts`, which went **2.0.0** to signal exactly this. Migrate against the Contracts
migration guide:

```csharp
// Before
@inject IPageState DisplayState
… DisplayState.PageTitlePrefix, DisplayState.PageTitleSeparator, DisplayState.PageTitleSuffix

// After
@inject IBrowserDocumentState DocumentState
… DocumentState.DocumentTitlePrefix, DocumentState.DocumentTitleSeparator, DocumentState.DocumentTitleSuffix
```

You cannot take this release without `Cirreum.Contracts` 2.0.0 and `Cirreum.Domain` 2.0.0 — those
majors are the honest warning, and this package's minor reflects only its own unchanged surface.

## What didn't change

- `DomainApplication.CreateBuilder(args)` and `BuildAndRunAsync()`
- `AddRemoteClient()`, `AddApplicationUserResolver()`, `AddDefaultAuthorization()`
- `CommonClaimsPrincipalFactory` and the `custom*` claim canonicalization shipped in 1.1.0
- The state/ViewModel component hierarchy and notification scoping
- Every component's rendered output, including `AppPageTitle`

## Coordinated downstream work

Part of the `Cirreum.Kernel` 2.0.0 wave. `Cirreum.Runtime.Wasm.Msal` and `Cirreum.Runtime.Wasm.Oidc`
re-pin onto this release; apps compose authentication through one of those, and neither changes its
own surface.

## See also

- [`CHANGELOG.md`](CHANGELOG.md)
