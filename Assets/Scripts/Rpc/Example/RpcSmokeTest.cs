using System.Threading.Tasks;
using UnityEngine;
using Game.Rpc.Contracts;
using Game.Rpc.Runtime;
using Game.Rpc.Runtime.GeneratedManual;

namespace Game.Rpc.Example
{
    public sealed class RpcSmokeTest : MonoBehaviour
    {
        private async void Start()
        {
            // Default: pure in-memory loopback so you can validate RPC plumbing without a server process.
            var cfg = new TransportConfig { Kind = TransportKind.Loopback };

            var clientTransport = TransportFactory.Create(cfg, out var serverTransport);
            var server = new RpcServer(serverTransport!);
            var impl = new PlayerServiceImpl();
            IPlayerServiceBinder.Bind(server, impl);

            await server.StartAsync();

            var client = new RpcClient(clientTransport);
            await client.StartAsync();

            var player = new IPlayerServiceClient(client);

            var reply = await player.LoginAsync(new LoginRequest { Account = "demo", Password = "pw" });
            Debug.Log($"LoginReply: Code={reply.Code}, Token={reply.Token}");

            await player.PingAsync();
            Debug.Log("Ping ok.");
        }

        private sealed class PlayerServiceImpl : IPlayerService
        {
            public ValueTask<LoginReply> LoginAsync(LoginRequest req)
                => new ValueTask<LoginReply>(new LoginReply { Code = 0, Token = "token-demo" });

            public ValueTask PingAsync() => default;
        }
    }
}
