using System.Collections;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using ULinkRPC.Runtime;
using Game.Rpc.Runtime.Generated;
using UnityEngine;
using UnityEngine.TestTools;
using NUnitAssert = NUnit.Framework.Assert;

namespace Tests.Editor.Rpc
{
    public class RpcLoopbackTests
    {
        [UnityTest]
        public IEnumerator Loopback_Login_Works()
        {
            var t = RunAsync();
            // UnityTest 里用 IEnumerator 等待 Task 完成
            yield return new WaitUntil(() => t.IsCompleted);

            // 如果 Task 内部抛异常，这里要把异常"抛出来"，否则测试会假通过
            if (t.IsFaulted) throw t.Exception;
        }


        private async Task RunAsync()
        {
            var cfg = new TransportConfig { Kind = TransportKind.Loopback };
            var clientTransport = TransportFactory.Create(cfg, out var serverTransport);

            var server = new RpcServer(serverTransport!);
            PlayerServiceBinder.Bind(server, new Impl());
            await server.StartAsync();

            var client = new RpcClient(clientTransport);
            await client.StartAsync();

            var proxy = new PlayerServiceClient(client);

            var reply = await proxy.LoginAsync(new LoginRequest { Account = "a", Password = "b" });

            NUnitAssert.AreEqual("ok", reply.Token);

            await proxy.PingAsync();
            Debug.Log("Ping ok.");

            var greeting = await proxy.ComposeGreetingAsync("Alice", 7, true);
            NUnitAssert.AreEqual("Hello Alice, Lv.7 [VIP]", greeting);

            // 可选：清理（避免测试进程残留后台任务）
            await server.StopAsync();
            await client.DisposeAsync();
        }

        private sealed class Impl : IPlayerService
        {
            public ValueTask<LoginReply> LoginAsync(LoginRequest req)
            {
                return new ValueTask<LoginReply>(new LoginReply { Code = 0, Token = "ok" });
            }

            public ValueTask PingAsync()
            {
                return default;
            }

            public ValueTask<string> ComposeGreetingAsync(string name, int level, bool vip)
            {
                var tag = vip ? "VIP" : "NORMAL";
                return new ValueTask<string>($"Hello {name}, Lv.{level} [{tag}]");
            }
        }
    }
}
