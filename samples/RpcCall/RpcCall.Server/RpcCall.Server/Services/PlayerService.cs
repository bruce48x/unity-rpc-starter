using Game.Rpc.Contracts;

namespace RpcCall.Server.Services;

public class PlayerService: IPlayerService
{
    public ValueTask<LoginReply> LoginAsync(LoginRequest req)
    {
        // Example: accept any account, return a dummy token.
        // Replace with your own auth logic.
        return new ValueTask<LoginReply>(new LoginReply
        {
            Code = 0,
            Token = $"token-{req.Account}-{Guid.NewGuid():N}"
        });
    }

    public ValueTask PingAsync()
    {
        return default;
    }
}
