# Unity RPC Starter (Unity 2022 LTS)

This repository is a starter skeleton for a **strongly-typed RPC** framework for **Unity + .NET**, designed to work with:
- iOS (IL2CPP) and HybridCLR (hot-update)
- Shared Contracts (`interface` + DTO) between client and server
- Transport switchability (TCP / WebSocket / KCP) behind a single abstraction
- MemoryPack for DTO serialization

## What is included
- `Assets/Scripts/Rpc/Contracts`: Attributes + example DTOs + example service interface
- `Assets/Scripts/Rpc/Runtime`: Transport abstraction + framing + RPC client/server cores
- `Assets/Scripts/Rpc/Transports`: TCP + ClientWebSocket transport, and a KCP **stub** transport
- `Assets/Scripts/Rpc/GeneratedManual`: manually-written client stub + server binder for the example service (until Source Generator is plugged in)

## NuGet installation
This project is wired to install NuGetForUnity via OpenUPM (see `Packages/manifest.json` and `Packages/packages-lock.json` once Unity resolves it).

## MemoryPack installation
This project is wired to install MemoryPack via NuGetForUnity.

## Source Generator (planned)
A Roslyn Source Generator project stub lives in `Tools/Rpc.SourceGen/`. Build it locally and add the analyzer DLL to Unity (Analyzer import),
then you can remove the manual stubs in `Assets/Scripts/Rpc/GeneratedManual`.

## Tests
1. Open the project in Unity 2022 LTS.
2. Ensure packages resolve (MemoryPack should be present).
3. open `Window` -> `General` -> `Test Runner` ， click `EditMode`, click `Run All`

See CONTRIBUTING.md for architecture, testing, and platform constraints.
