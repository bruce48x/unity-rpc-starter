# Game RPC Server

与 [Unity RPC Starter](../README.md) 客户端配套的 **TCP / WebSocket 服务端**，实现与 Unity RPC 相同的协议与契约，便于本地联调与集成测试。

## 协议与契约（与 Unity 一致）

- **Framing**：4 字节大端 `uint32` 长度前缀 + 荷载（与 `Game.Rpc.Runtime.LengthPrefix` 一致）
- **信封**：`RpcRequestEnvelope` / `RpcResponseEnvelope`，MemoryPack 序列化
- **示例服务**：`IPlayerService`（ServiceId=1）
  - `LoginAsync(LoginRequest)` → `LoginReply`（MethodId=1）
  - `PingAsync()` → void（MethodId=2）

## 构建与运行

```bash
cd server
dotnet build
dotnet run --project Game.Rpc.Server
```

指定端口（默认 TCP 20000、WS 20001、KCP 20002）：

参数顺序：`tcpPort wsPort wsHost kcpPort`

```bash
dotnet run --project Game.Rpc.Server -- 20000 20001 127.0.0.1 20002
```

指定 WS 绑定地址（默认 127.0.0.1）：

```bash
dotnet run --project Game.Rpc.Server -- 20000 20001 0.0.0.0
```

## Unity 客户端配置

在 Unity 中配置 `TransportConfig` 使用 Tcp：

- `Kind = TransportKind.Tcp`
- `Host = "127.0.0.1"`（或本机 IP / 服务器 IP）
- `Port = 20000`（与 `dotnet run` 所用端口一致）

使用 WebSocket：

- `Kind = TransportKind.WebSocket`
- `WsUrl = "ws://127.0.0.1:20001/rpc"`

使用 KCP：

- `Kind = TransportKind.Kcp`

## 项目结构

| 目录 / 文件 | 说明 |
|-------------|------|
| `Contracts/` | `IPlayerService`、`LoginRequest`/`LoginReply`、`RpcAttributes`，与 `Packages/com.bruce.rpc.contracts` 结构一致 |
| `Runtime/` | `RpcEnvelopes`、`LengthPrefix`、`ITransport`、`RpcServer`，与 Unity 的 `Game.Rpc.Runtime` 协议一致 |
| `Transports/` | `TcpServerTransport` / `WebSocketServerTransport` / `KcpServerTransport`，实现 `ITransport` |
| `Binder/` | `PlayerServiceBinder`，将 `IPlayerService` 实现注册到 `RpcServer` |
| `Program.cs` | TCP + WebSocket accept 循环，每连接一个 `RpcServer` 并绑定 `IPlayerService` |

## 扩展服务与认证

- **新增 RPC 方法**：在 `IPlayerService` 或新接口上添加方法，在 `PlayerServiceBinder`（或新 Binder）中注册；Unity 端需同步更新 Contracts 与 Generated 的 Client/Binder。
- **认证**：在 `PlayerServiceImpl.LoginAsync` 中接入你的账号、密码校验与 Token 签发逻辑；可将 `Token` 写入 `LoginReply`，由客户端在后续请求中按你们的约定携带。

## 依赖

- .NET 8.0
- [MemoryPack](https://github.com/Cysharp/MemoryPack) 1.21.4（与 Unity 端版本一致，保证序列化兼容）
- [Kcp](https://www.nuget.org/packages/Kcp) 2.7.0（服务端 KCP 传输）

## 参考

- [CONTRIBUTING.md](../CONTRIBUTING.md)：架构、传输、测试等约定
- `Assets/Scripts/Rpc/`：Unity 端 Runtime、Transports、Generated
- `Packages/com.bruce.rpc.contracts/`：Contracts
