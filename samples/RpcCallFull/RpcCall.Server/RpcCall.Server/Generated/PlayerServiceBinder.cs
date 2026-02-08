using System;
using Game.Rpc.Contracts;
using ULinkRPC.Runtime;

namespace RpcCall.Server.Generated
{
    public static class PlayerServiceBinder
    {
        private const int ServiceId = 1;

        public static void Bind(RpcServer server, IPlayerService impl)
        {
            server.Register(ServiceId, 1, async (req, ct) =>
            {
                var arg1 = server.Serializer.Deserialize<LoginRequest>(req.Payload.AsSpan())!;
                var resp = await impl.LoginAsync(arg1);
                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = server.Serializer.Serialize(resp) };
            });

            server.Register(ServiceId, 2, async (req, ct) =>
            {
                await impl.PingAsync();
                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = server.Serializer.Serialize(RpcVoid.Instance) };
            });

            server.Register(ServiceId, 3, async (req, ct) =>
            {
                var (arg1, arg2, arg3) = server.Serializer.Deserialize<(string, int, bool)>(req.Payload.AsSpan())!;
                var resp = await impl.ComposeGreetingAsync(arg1, arg2, arg3);
                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = server.Serializer.Serialize(resp) };
            });

        }
    }
}
