using Game.Rpc.Contracts;
using Game.Rpc.Runtime;
using Game.Rpc.Runtime.GeneratedManual;
using NUnit.Framework;
using System.Threading.Tasks;

using NUnitAssert = NUnit.Framework.Assert;
public class RpcLoopbackTests
{
    private sealed class Impl : IPlayerService
    {
        public ValueTask<LoginReply> LoginAsync(LoginRequest req)
        {
            return new ValueTask<LoginReply>(new LoginReply { Code = 0, Token = "ok" });
        }

        public ValueTask PingAsync() => default;
    }

    [Test]
    public async Task Loopback_Login_Works()
    {
        var cfg = new TransportConfig { Kind = TransportKind.Loopback };

        var clientTransport = TransportFactory.Create(cfg, out var serverTransport);

        var server = new RpcServer(serverTransport!);
        IPlayerServiceBinder.Bind(server, new Impl());
        await server.StartAsync();

        var client = new RpcClient(clientTransport);
        await client.StartAsync();

        var proxy = new IPlayerServiceClient(client);

        var reply = await proxy.LoginAsync(new LoginRequest { Account = "a", Password = "b" });

        NUnitAssert.AreEqual("ok", reply.Token);
    }
}
