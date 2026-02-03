# ULinkRPC Server

与 [ULinkRPC](../../../README.md) 客户端配套的 **TCP 服务端**，实现与 Unity RPC 相同的协议与契约，便于本地联调与集成测试。
此最简示例仅保留 TCP。

## 协议与契约（与 Unity 一致）

- **Framing**：4 字节大端 `uint32` 长度前缀 + 荷载（与 `Game.Rpc.Runtime.LengthPrefix` 一致）
- **信封**：`RpcRequestEnvelope` / `RpcResponseEnvelope`，MemoryPack 序列化
- **示例服务**：`IPlayerService`（ServiceId=1）
  - `LoginAsync(LoginRequest)` → `LoginReply`（MethodId=1）
  - `PingAsync()` → void（MethodId=2）

## 构建与运行

```bash
cd samples/RpcCallLite/RpcCall.Server
dotnet build
dotnet run --project RpcCall.Server
```

指定端口（默认 TCP 20000）：

```bash
dotnet run --project RpcCall.Server -- 20000
```

## 传输安全（压缩 / 加密）

此最简示例不包含压缩与加密，保持流程清晰易懂。

## Unity 客户端配置

在 Unity 中配置 `TransportConfig` 使用 Tcp：

- `Kind = TransportKind.Tcp`
- `Host = "127.0.0.1"`（或本机 IP / 服务器 IP）
- `Port = 20000`（与 `dotnet run` 所用端口一致）

WebSocket / KCP 的联调示例已移到更完整的 `samples/RpcCallFull`。

## 项目结构

| 目录 / 文件 | 说明 |
|-------------|------|
| `samples/RpcCallLite/RpcCall.Unity/Packages/com.bruce.rpc.contracts/` | `IPlayerService`、`LoginRequest`/`LoginReply`、`RpcAttributes`，服务端通过源码引用使用 |
| `src/ULinkRPC.Runtime/` | `RpcEnvelopes`、`LengthPrefix`、`ITransport`、`RpcServer` 与传输实现（TCP/WS/KCP） |
| `samples/RpcCallLite/RpcCall.Server/RpcCall.Server/Generated/` | 服务端 Binder 与 `AllServicesBinder` |
| `samples/RpcCallLite/RpcCall.Server/RpcCall.Server/Program.cs` | TCP accept 循环，每连接一个 `RpcServer` 并绑定 `IPlayerService` |

## 扩展服务与认证

- **新增 RPC 方法**：在 `IPlayerService` 或新接口上添加方法，通过代码生成更新 Server Binder；Unity 端需同步更新 Contracts 与 Generated 的 Client/Binder。
- **认证**：在 `PlayerServiceImpl.LoginAsync` 中接入你的账号、密码校验与 Token 签发逻辑；可将 `Token` 写入 `LoginReply`，由客户端在后续请求中按你们的约定携带。

## 依赖

- .NET 8.0
- [MemoryPack](https://github.com/Cysharp/MemoryPack) 1.21.4（与 Unity 端版本一致，保证序列化兼容）

## Runtime 包

- `ULinkRPC.Runtime`（netstandard2.1 + net8.0）
- Contracts 为 Git 项目，不通过 NuGet 发布

## 代码生成

RPC 客户端与 Unity 测试 Binder 由工具生成（本地源码运行或 dotnet tool 均可）：

```bash
dotnet run --project src/ULinkRPC.CodeGen/ULinkRPC.CodeGen.csproj --
# 或
ulinkrpc-codegen
```

输出路径：
- `samples/RpcCallLite/RpcCall.Unity/Assets/Scripts/Rpc/RpcGenerated/`（Unity 客户端）
- `samples/RpcCallLite/RpcCall.Unity/Assets/Scripts/Rpc/RpcGenerated/`（Unity EditMode 测试 Binder）
- `samples/RpcCallLite/RpcCall.Server/RpcCall.Server/Generated/`（服务端 Binder 与 `AllServicesBinder`）

## 参考

- [CONTRIBUTING.md](../../../CONTRIBUTING.md)：架构、传输、测试等约定
- `samples/RpcCallLite/RpcCall.Unity/Assets/Scripts/Rpc/`：Unity 端 Runtime、Transports、Generated
- `samples/RpcCallLite/RpcCall.Unity/Packages/com.bruce.rpc.contracts/`：Contracts

