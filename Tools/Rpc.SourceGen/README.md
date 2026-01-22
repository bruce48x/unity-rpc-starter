# Rpc.SourceGen (placeholder)

This folder is a placeholder for a Roslyn Incremental Source Generator that will:
- Scan [RpcService] interfaces
- Generate strongly-typed client stubs and server binders
- Enforce "0 or 1 parameter" rule at compile-time

Build it locally with .NET SDK and import the produced analyzer DLL into Unity (Asmdef -> Roslyn Analyzers).
