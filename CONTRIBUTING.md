# Contributing Guide

This document defines **mandatory engineering rules** for contributing to this repository.

This project targets **Unity 2022 LTS** with **iOS / IL2CPP / HybridCLR** in mind.
Stability, predictability, and platform compatibility take priority over convenience APIs.

---

## 1. Project Architecture Rules

### Assembly boundaries (asmdef)
The project is intentionally split into multiple assemblies:

- `Game.Rpc.Contracts`
  - RPC interfaces
  - DTOs
  - Attributes (`RpcService`, `RpcMethod`)
  - **No UnityEngine dependency**
- `Game.Rpc.Runtime`
  - Transport abstraction
  - Framing
  - RPC client/server core
- `Game.Rpc.Transports`
  - TCP / WebSocket / KCP (stub) implementations
- Test assemblies (`*.Tests.Editor`, `*.Tests.PlayMode`)
  - NUnit + Unity Test Framework only

Do **NOT** introduce circular dependencies between assemblies.

---

## 2. Platform Constraints (Unity / iOS / IL2CPP)

Because this project targets **iOS + IL2CPP**, the following APIs are **forbidden** anywhere in Unity client code (including tests):

- `System.Threading.Channels`
- `System.IO.Pipelines`
- `System.Reflection.Emit`
- Runtime code generation
- APIs that rely on JIT-only behavior

If an API is commonly used on server-side .NET but not explicitly supported by Unity IL2CPP, assume it is **unsafe** unless proven otherwise.

---

## 3. Async & ValueTask Rules

### ValueTask usage
`ValueTask` is preferred over `Task` for performance reasons, but **only a safe subset is allowed**.

#### Allowed patterns
- `return default;` for `ValueTask`
- `return new ValueTask<T>(value);` for `ValueTask<T>`
- `async` methods returning `ValueTask<T>` with `return value;`

#### Forbidden patterns
- `ValueTask.CompletedTask`
- `ValueTask.FromResult(...)`

These APIs may be missing in Unity’s .NET profile and must not be used.

---

## 4. Transport & Networking Rules

- All transports MUST implement `ITransport`.
- RPC code MUST NOT depend on a specific transport implementation.
- Transport implementations must:
  - Be cancellation-safe
  - Avoid background thread leaks
  - Be explicit about disconnect behavior

For testing and local validation, prefer **LoopbackTransport**.

---

## 5. Testing Rules (Very Important)

### Test framework
- All tests use **NUnit + Unity Test Framework**
- Tests must live under `Assets/Tests/**`

### EditMode vs PlayMode
- **EditMode tests** are the default for RPC/runtime logic.
- PlayMode tests are only for:
  - MonoBehaviour lifecycle
  - Scene integration
  - Platform-specific behavior

### Test assembly requirements
- Test assemblies MUST be marked as test assemblies via:
  ```json
  "optionalUnityReferences": ["TestAssemblies"]
  ```
- EditMode test assemblies MUST restrict platforms:
  ```json
  "includePlatforms": ["Editor"]
  ```

---

## 6. Async Test Rule (Critical)

Due to Unity Test Runner and NUnit compatibility constraints:

❌ **DO NOT** write async tests as:
```csharp
[Test]
public async Task MyTest() { ... }
```

✅ **All async tests MUST use**:
- `[UnityTest]`
- `IEnumerator`
- Internal `Task` + `WaitUntil`

### Required async test pattern
```csharp
[UnityTest]
public IEnumerator Example_Async_Test()
{
    var task = RunAsync();

    yield return new WaitUntil(() => task.IsCompleted);

    if (task.IsFaulted)
        throw task.Exception!;
}

private async Task RunAsync()
{
    // async test logic
}
```

This rule applies to **both EditMode and PlayMode tests**.

---

## 7. Assertion Rules

Because Unity injects its own `Assert`, ambiguity must be avoided.

### Mandatory rule
Every test file MUST include:
```csharp
using NUnitAssert = NUnit.Framework.Assert;
```

All assertions MUST use:
```csharp
NUnitAssert.*
```

❌ Do NOT use:
- `Assert.*` (unqualified)
- `UnityEngine.Assertions.Assert`

---

## 8. Code Style & Safety

- Prefer explicit lifetimes (`DisposeAsync`, `StopAsync`)
- Clean up background loops in tests
- Avoid implicit global state
- Favor clarity over micro-optimizations in shared infrastructure code

---

## 9. Code Generation Policy

RPC client stubs and server binders are **auto-generated** in the Unity Editor when contracts change.
You can also run the menu manually: **`Tools` → `RPC` → `Generate RPC Code`**.

- Generated files are written to `Assets/Scripts/Rpc/GeneratedManual/` and **must be committed**.
- **DO NOT** manually edit generated files; they will be overwritten.
- Generated code MUST:
  - Be deterministic
  - Avoid reflection-heavy logic
  - Be compatible with IL2CPP AOT

---

## 10. AI / Code Assistant Notes

When using AI tools (Claude Code, Cursor, Codex, etc.):

- Always respect the rules in this document.
- If a generated change violates Unity / IL2CPP constraints, it must be corrected before committing.
- Prefer changes that preserve existing assembly boundaries and test coverage.

See `.cursor/rules/` for enforced Cursor-specific rules.
