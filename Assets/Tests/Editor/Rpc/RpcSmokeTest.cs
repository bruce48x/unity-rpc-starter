using System.Collections;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using Game.Rpc.Runtime;
using Game.Rpc.Runtime.Generated;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Editor.Rpc
{
    public sealed class RpcSmokeTest : MonoBehaviour
    {
        [UnityTest]
        public IEnumerator Smoke_Test_Works()
        {
            var t = RunAsync();
            // UnityTest 里用 IEnumerator 等待 Task 完成
            yield return new WaitUntil(() => t.IsCompleted);

            // 如果 Task 内部抛异常，这里要把异常"抛出来"，否则测试会假通过
            if (t.IsFaulted) throw t.Exception;
        }

        private async Task RunAsync()
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
            {
                return new ValueTask<LoginReply>(new LoginReply { Code = 0, Token = "token-demo" });
            }

            public ValueTask PingAsync()
            {
                return default;
            }
        }
    }
}