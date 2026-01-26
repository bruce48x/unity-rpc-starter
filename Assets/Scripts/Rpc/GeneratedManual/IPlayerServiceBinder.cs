using Game.Rpc.Contracts;
using Game.Rpc.Runtime;
using MemoryPack;

namespace Game.Rpc.Runtime.GeneratedManual
{
    public static class IPlayerServiceBinder
    {
        private const int ServiceId = 1;

        public static void Bind(RpcServer server, IPlayerService impl)
        {
            server.Register(ServiceId, 1, async (req, ct) =>
            {
                var arg = MemoryPackSerializer.Deserialize<Game.Rpc.Contracts.LoginRequest>(req.Payload)!;
                var resp = await impl.LoginAsync(arg);
                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = MemoryPackSerializer.Serialize(resp) };
            });

            server.Register(ServiceId, 2, async (req, ct) =>
            {
                await impl.PingAsync();
                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = MemoryPackSerializer.Serialize(RpcVoid.Instance) };
            });

        }
    }
}
