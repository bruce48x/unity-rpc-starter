using System.Threading.Tasks;
using ULinkRPC.Runtime;

namespace Game.Rpc.Contracts
{
    [RpcService(1)]
    public interface IPlayerService
    {
        [RpcMethod(1)]
        ValueTask<LoginReply> LoginAsync(LoginRequest req);

        [RpcMethod(2)]
        ValueTask PingAsync();

        [RpcMethod(3)]
        ValueTask<string> ComposeGreetingAsync(string name, int level, bool vip);
    }
}
