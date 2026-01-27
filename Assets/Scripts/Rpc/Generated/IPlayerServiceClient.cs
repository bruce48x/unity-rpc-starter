using System.Threading;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using Game.Rpc.Runtime;

namespace Game.Rpc.Runtime.Generated
{
    public sealed class IPlayerServiceClient : IPlayerService
    {
        private const int ServiceId = 1;
        private readonly RpcClient _client;

        public IPlayerServiceClient(RpcClient client) { _client = client; }

        public async ValueTask<LoginReply> LoginAsync(LoginRequest req)
        {
            return await _client.CallAsync<LoginRequest, LoginReply>(ServiceId, 1, req, CancellationToken.None);
        }

        public async ValueTask PingAsync()
        {
            await _client.CallAsync<RpcVoid, RpcVoid>(ServiceId, 2, RpcVoid.Instance, CancellationToken.None);
        }

    }
}
