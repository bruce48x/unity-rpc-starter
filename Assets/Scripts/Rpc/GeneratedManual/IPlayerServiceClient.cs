using System.Threading;
using System.Threading.Tasks;
using Game.Rpc.Contracts;

namespace Game.Rpc.Runtime.GeneratedManual
{
    /// <summary>
    ///     Temporary hand-written client stub until Roslyn Source Generator is integrated.
    /// </summary>
    public sealed class IPlayerServiceClient : IPlayerService
    {
        private const int ServiceId = 1;
        private readonly RpcClient _client;

        public IPlayerServiceClient(RpcClient client)
        {
            _client = client;
        }

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