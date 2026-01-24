using System.Threading.Tasks;

namespace Game.Rpc.Contracts
{
    [RpcService(1)]
    public interface IPlayerService
    {
        [RpcMethod(1)]
        ValueTask<LoginReply> LoginAsync(LoginRequest req);

        [RpcMethod(2)]
        ValueTask PingAsync();
    }
}