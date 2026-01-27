using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using Game.Rpc.Runtime;
using Game.Rpc.Runtime.Generated;

const int DefaultPort = 20000;

var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : DefaultPort;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var listener = new TcpListener(IPAddress.Any, port);
listener.Start();
Console.WriteLine($"Game RPC Server listening on 0.0.0.0:{port}. Press Ctrl+C to stop.");

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        TcpClient client;
        try
        {
            client = await listener.AcceptTcpClientAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            break;
        }

        _ = RunConnectionAsync(client, cts.Token);
    }
}
finally
{
    listener.Stop();
    Console.WriteLine("Server stopped.");
}

static async Task RunConnectionAsync(TcpClient client, CancellationToken hostCt)
{
    var remote = client.Client.RemoteEndPoint?.ToString() ?? "?";
    Console.WriteLine($"[{remote}] Connected.");

    ITransport? transport = null;
    RpcServer? server = null;

    try
    {
        transport = new TcpServerTransport(client);
        server = new RpcServer(transport);

        IPlayerServiceBinder.Bind(server, new PlayerServiceImpl());
        await server.StartAsync(hostCt).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        // Host shutdown
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{remote}] Error: {ex.Message}");
    }
    finally
    {
        if (server is not null)
            await server.StopAsync().ConfigureAwait(false);

        if (transport is not null)
            await transport.DisposeAsync().ConfigureAwait(false);
    }

    Console.WriteLine($"[{remote}] Disconnected.");
}

/// <summary>
///     Example IPlayerService implementation for the server.
/// </summary>
internal sealed class PlayerServiceImpl : IPlayerService
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

    public ValueTask PingAsync() => default;
}
