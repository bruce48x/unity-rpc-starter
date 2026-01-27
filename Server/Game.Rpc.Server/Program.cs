using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using Game.Rpc.Runtime;
using Game.Rpc.Runtime.Generated;

const int DefaultTcpPort = 20000;
const int DefaultWsPort = 20001;
const string DefaultWsHost = "127.0.0.1";

var tcpPort = DefaultTcpPort;
var wsPort = DefaultWsPort;
var wsHost = DefaultWsHost;

if (args.Length > 0 && int.TryParse(args[0], out var p))
    tcpPort = p;
if (args.Length > 1 && int.TryParse(args[1], out var wp))
    wsPort = wp;
if (args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]))
    wsHost = args[2];

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Game RPC Server TCP listening on 0.0.0.0:{tcpPort}. Press Ctrl+C to stop.");
Console.WriteLine($"Game RPC Server WS listening on ws://{wsHost}:{wsPort}/rpc.");

var tcpTask = RunTcpListenerAsync(tcpPort, cts.Token);
var wsTask = RunWebSocketListenerAsync(wsHost, wsPort, cts.Token);

try
{
    await Task.WhenAll(tcpTask, wsTask).ConfigureAwait(false);
}
finally
{
    Console.WriteLine("Server stopped.");
}

static async Task RunTcpListenerAsync(int port, CancellationToken hostCt)
{
    var listener = new TcpListener(IPAddress.Any, port);
    listener.Start();

    try
    {
        while (!hostCt.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(hostCt).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            _ = RunConnectionAsync(new TcpServerTransport(client), client.Client.RemoteEndPoint?.ToString() ?? "?", hostCt);
        }
    }
    finally
    {
        listener.Stop();
    }
}

static async Task RunWebSocketListenerAsync(string host, int port, CancellationToken hostCt)
{
    var listener = new HttpListener();
    listener.Prefixes.Add(BuildWsPrefix(host, port));
    listener.Start();

    using var reg = hostCt.Register(() =>
    {
        try { listener.Stop(); } catch { }
    });

    try
    {
        while (!hostCt.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (!ctx.Request.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
                continue;
            }

            _ = HandleWebSocketClientAsync(ctx, hostCt);
        }
    }
    finally
    {
        listener.Close();
    }
}

static async Task HandleWebSocketClientAsync(HttpListenerContext ctx, CancellationToken hostCt)
{
    var remote = ctx.Request.RemoteEndPoint?.ToString() ?? "?";
    Console.WriteLine($"[{remote}] WS Connected.");

    try
    {
        var wsContext = await ctx.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
        await RunConnectionAsync(new WebSocketServerTransport(wsContext.WebSocket), remote, hostCt).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{remote}] WS Error: {ex.Message}");
        try
        {
            ctx.Response.StatusCode = 500;
            ctx.Response.Close();
        }
        catch { }
    }
    finally
    {
        Console.WriteLine($"[{remote}] WS Disconnected.");
    }
}

static string BuildWsPrefix(string host, int port)
{
    if (string.IsNullOrWhiteSpace(host) || host == "*" || host == "+" || host == "0.0.0.0")
        return $"http://+:{port}/rpc/";

    return $"http://{host}:{port}/rpc/";
}

static async Task RunConnectionAsync(ITransport transport, string remote, CancellationToken hostCt)
{
    RpcServer? server = null;

    try
    {
        server = new RpcServer(transport);

        IPlayerServiceBinder.Bind(server, new PlayerServiceImpl());
        await server.StartAsync(hostCt).ConfigureAwait(false);
        await server.WaitForCompletionAsync().ConfigureAwait(false);
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
