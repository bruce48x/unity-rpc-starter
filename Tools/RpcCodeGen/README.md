# ULinkRPC.CodeGen

Command-line code generator for ULinkRPC.

## Install (dotnet tool)

```bash
dotnet tool install -g ULinkRPC.CodeGen
```

## Usage

```bash
ulinkrpc-codegen [options]
```

### Modes

- `auto` (default): detect project type and generate outputs accordingly.
- `unity`: generate Unity client + binder code.
- `server`: generate server binders + `AllServicesBinder`.

### Options

- `--contracts <path>` Path to contract sources.
- `--output <path>` Output directory for generated clients (Unity).
- `--binder-output <path>` Output directory for generated binders (Unity).
- `--server-output <path>` Output directory for server binders.
- `--server-namespace <ns>` Namespace for server binders.
- `--mode <auto|unity|server>` Force output mode.

## Default Behavior

- Unity project: generates to `Assets/Scripts/Rpc/RpcGenerated`.
- Server project: generates to `Server/Game.Rpc.Server/Generated`.

Paths can be overridden via options.
