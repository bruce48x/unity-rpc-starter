using System.Threading.Tasks;

namespace Game.Rpc.Contracts
{
    [RpcService(serviceId: 1)]
    public interface IPlayerService
    {
        [RpcMethod(methodId: 1)]
        ValueTask<LoginReply> LoginAsync(LoginRequest req);

        [RpcMethod(methodId: 2)]
        ValueTask PingAsync();
    }
}
