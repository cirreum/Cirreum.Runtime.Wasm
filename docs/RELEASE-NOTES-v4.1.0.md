# Cirreum.Runtime.Wasm 4.1.0 — the policy you widened is the policy you see

## Why this release exists

3.0.0 made a Content-Security-Policy part of every Blazor WebAssembly publish. That settled
the default. It left the other half of the job undone: an application has to widen the
baseline for its API, its identity provider, its CDN, and nothing told it when a widening
mattered. A policy file that added `'unsafe-eval'` to `script-src`, or a bare `*` to
`connect-src`, published as quietly as one that added a font host. A keyword written without
quotes published too, as a host name nobody owns, and the policy read as hardened while
granting nothing.

The generator also had no tests. Four fixes shipped against it across 3.0.0 and 4.0.4, each
found in a deployed app rather than a build.

The second thread in this release is the browser interop. A review of `JSAppModule` against
the module it calls in `Cirreum.Components.WebAssembly` found the two sides disagreeing on
types and shapes in ways nothing checked, and this runtime held the consumer-side half of the
defects. Those ship here, against Components 2.0.0.

## What's new

**Risk-based CSP diagnostics.** An app or environment policy addition that materially widens
the posture produces an MSBuild warning with a stable code, attributed to the policy file and
directive that made it:

```
Properties\csp.policy : warning CSP0001: 'script-src' adds 'unsafe-eval'. This permits script
the policy cannot attribute to a file or hash, which removes most of the cross-site-scripting
protection a Content-Security-Policy provides.
```

Unsafe keywords on script directives, broad wildcards, scheme-only sources, any widening of
`object-src`, `base-uri` or `default-src`, third-party script origins and static nonces warn.
Routine additions, an API origin, a CDN, an identity provider's frame, a subdomain wildcard,
`data:` images, produce nothing, and the framework's own baseline is never reported. The build
does not fail over policy content. The codes are the posture control: `NoWarn` accepts one,
`WarningsAsErrors` makes one fatal in a deployment build. The two overlay-selection warnings
in the targets carry codes too (`CSP0101`, `CSP0102`), so a team that requires an environment
overlay in CI gets that gate from the same mechanism.

**Policy syntax that cannot mean what it says fails the build.** `script-src unsafe-inline`
is a host named `unsafe-inline` to the browser; generation stops with `CSP0901` and names the
file. Unknown directives, sources on a valueless directive and unknown quoted keywords warn,
since browsers ignore all three silently.

**The generated config is what the browser receives.** `'none'` is dropped from a directive
that gains other sources rather than emitted beside them. `report-to` is replaced, not
unioned. Sources are sorted ordinally, so two build agents with different cultures emit the
same bytes and a deployment diff shows only real policy changes. The finished
`staticwebapp.config.json` is parsed before it is written, so a config Static Web Apps would
reject fails the build that produced it, not the deployment.

**Tests.** A suite drives the shipped generator script as a process under both `pwsh` and
Windows PowerShell 5.1 and covers merging, normalization, every diagnostic code, inline-script
hashing, the opt-out and both output guards.

## The interop consumer

`JSAppModule` routes every module call through a guarded accessor, so a call before
`InitializeAsync` fails with a stated cause rather than a null reference. `GetFooterHeight`
reads a `double`; the JavaScript returns `offsetHeight` plus parsed margins, which is a
fraction for any rem-based margin, and the `int` it was read as throws on a fraction. Three
void setters stop reading an `int` from functions that return nothing. The rect accessors
return `DomRect?`, which is what the JavaScript always returned.

`DefaultThemeMonitor` decides whether to follow a system theme change from `IThemeState.Mode`,
not from a value the JavaScript read out of storage. On a fresh browser that value was `null`,
so an OS theme switch while the app was open was ignored until the user picked a mode.

A contract test reads the exported functions of `cirreum-interop.ts` from the sibling
Components checkout and checks that every identifier this runtime invokes is exported with a
compatible argument count, and that every export is invoked. The drift that produced these
defects now fails a test instead of a user.

## Compatibility

No application code changes. An app that publishes today publishes the same policy tomorrow,
with warnings where it widened something that matters; the only hard failures are the three
output guards, none of which a correct policy file trips. `IJSAppModule` and
`IThemeModeChangedRef` changed shape in Components 2.0.0; this runtime implements the new
contract, so an app that reaches the components through the runtime compiles unchanged. An app
that implements `IThemeModeChangedRef` itself follows the Components migration guide.

## See also

- `Cirreum.Components.WebAssembly` 2.0.0 — the interop contract and module this runtime
  consumes, and the component fixes that ride with it: keyboard-activated menus, the DataGrid
  column chooser toggle, links inside DataGrid rows.
- README, "Azure Static Web Apps configuration" — the full table of diagnostic codes and the
  suppression and promotion pattern.
