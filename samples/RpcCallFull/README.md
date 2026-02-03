# RpcCallFull (Full Tutorial)

完整 RPC 示例，包含压缩与加密。

## 结构

- `RpcCall.Server`：.NET 8 TCP 服务端
- `RpcCall.Unity`：Unity 2022 LTS 客户端

## 快速开始

1. 运行服务端

```bash
cd samples/RpcCallFull/RpcCall.Server
dotnet build
dotnet run --project RpcCall.Server
```

2. 打开 Unity 项目

打开 `samples/RpcCallLite/RpcCall.Unity`，进入场景 `Assets/Scenes/TcpConnectionTest.unity`，点击 Play。

默认会自动连接 `127.0.0.1:20000` 并执行 `Login` + `Ping`。
