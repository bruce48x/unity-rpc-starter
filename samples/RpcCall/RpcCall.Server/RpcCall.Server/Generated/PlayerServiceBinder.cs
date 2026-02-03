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
                var arg = server.Serializer.Deserialize<LoginRequest>(req.Payload.AsSpan())!;
                var resp = await impl.LoginAsync(arg);
                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = server.Serializer.Serialize(resp) };
            });

            server.Register(ServiceId, 2, async (req, ct) =>
            {
                await impl.PingAsync();
                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = server.Serializer.Serialize(RpcVoid.Instance) };
            });

        }
    }
}

