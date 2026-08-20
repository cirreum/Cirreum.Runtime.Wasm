# Cirreum.Runtime.Wasm 2.1.0 — the client states what it knows

## Why this release exists

Two loose ends from the attribute-authority work close here, both about the client saying
plainly what it already knows.

The `custom*` canonicalization mechanism was relocated into `Cirreum.Kernel` in 2.1.0 so both
runtimes could share it, but this runtime kept using its own local copy — two implementations
of a wire convention that only works if both sides agree on it. And `IUserState.SubjectKind`
reported `Unknown` for a signed-in browser user, which is not a missing answer so much as an
unstated one: a WebAssembly session always has a person at it.

## What's new

**`SubjectKind.Human` on the client user.** An authenticated client user now reports
`SubjectKind.Human`; an anonymous one reports `Unknown`. Unlike the authentication boundary —
which depends on which scheme authenticated the caller, and so is a server-side fact — this one
is knowable without any scheme declaration. `IsHumanSubject` answers true client-side, matching
what the server resolves for the same session.

**Canonicalization comes from the Kernel.** The local copy is deleted and the principal factory
calls the shared mechanism with the client posture:

```csharp
CustomClaimCanonicalizer.Canonicalize(identity, excludeRoles: false);
```

`excludeRoles: false` is the client's answer to a question the Kernel now makes every caller
state. The browser aliases minted roles because the token is the only source of roles it has,
and client role claims gate *rendering* — every operation is re-authorized on the server, where
roles come from whichever side the scheme declares authoritative. The server passes `true` for
the same reason in reverse.

Behaviour is unchanged: the local type was internal, and the call it replaces did exactly this.
The canonicalizer's twenty-test suite moved to the Kernel with the type.

## Compatibility

- No application surface changes. The deleted canonicalizer was internal to this package.
- `IUserState.SubjectKind` changes value for authenticated client users (`Unknown` → `Human`).
  Code branching on `SubjectKind` or `IsHumanSubject` client-side now gets the accurate answer;
  nothing that previously read `Unknown` as "not a machine" changes meaning, since
  `IsMachineSubject` was and remains false.

## See also

- `Cirreum.Kernel 2.2.0` — the shared canonicalization mechanism and its posture parameter.
- `Cirreum.Runtime.AuthenticationProvider 2.1.0` — the server posture: canonicalization
  excluding roles, and role claims resolved from the scheme's declared authority.
