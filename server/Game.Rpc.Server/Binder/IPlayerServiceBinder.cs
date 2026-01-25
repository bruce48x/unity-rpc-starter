using Game.Rpc.Contracts;
using MemoryPack;

namespace Game.Rpc.Runtime.GeneratedManual
{
    /// <summary>
    ///     Server binder for IPlayerService. Mirrors Assets/Scripts/Rpc/GeneratedManual/IPlayerServiceBinder
    ///     for wire compatibility with the Unity RPC client.
    /// </summary>
    public static class IPlayerServiceBinder
    {
        private const int ServiceId = 1;

        public static void Bind(RpcServer server, IPlayerService impl)
        {
            server.Register(ServiceId, 1, async (req, ct) =>
            {
                var arg = MemoryPackSerializer.Deserialize<LoginRequest>(req.Payload)!;
                var resp = await impl.LoginAsync(arg);
                return new RpcResponseEnvelope
                {
                    RequestId = req.RequestId,
                    Status = RpcStatus.Ok,
                    Payload = MemoryPackSerializer.Serialize(resp)
                };
            });

            server.Register(ServiceId, 2, async (req, ct) =>
            {
                await impl.PingAsync();
                return new RpcResponseEnvelope
                {
                    RequestId = req.RequestId,
                    Status = RpcStatus.Ok,
                    Payload = MemoryPackSerializer.Serialize(RpcVoid.Instance)
                };
            });
        }
    }
}
