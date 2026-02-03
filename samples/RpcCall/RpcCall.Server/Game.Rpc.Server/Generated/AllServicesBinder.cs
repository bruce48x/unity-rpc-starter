using Game.Rpc.Contracts;
using ULinkRPC.Runtime;

namespace Game.Rpc.Server.Generated
{
    public static class AllServicesBinder
    {
        public static void BindAll(RpcServer server, IPlayerService playerService)
        {
            PlayerServiceBinder.Bind(server, playerService);
        }
    }
}
