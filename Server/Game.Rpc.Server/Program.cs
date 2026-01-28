using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Game.Rpc.Contracts;
using Game.Rpc.Runtime;
using Game.Rpc.Runtime.Generated;

const int DefaultTcpPort = 20000;
const int DefaultWsPort = 20001;
const int DefaultKcpPort = 20002;
const string DefaultWsHost = "127.0.0.1";

var tcpPort = DefaultTcpPort;
var wsPort = DefaultWsPort;
var kcpPort = DefaultKcpPort;
var wsHost = DefaultWsHost;
var security = new TransportSecurityConfig();

var positional = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    var arg = args[i];
    if (!arg.StartsWith("--", StringComparison.Ordinal))
    {
        positional.Add(arg);
        continue;
    }

    if (arg.StartsWith("--compress", StringComparison.OrdinalIgnoreCase))
    {
        security.EnableCompression = true;
        var parts = arg.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[1], out var threshold))
            security.CompressionThresholdBytes = threshold;
        continue;
    }

    if (arg.StartsWith("--compress-threshold", StringComparison.OrdinalIgnoreCase))
    {
        security.EnableCompression = true;
        if (TryReadNext(args, ref i, out var value) && int.TryParse(value, out var threshold))
            security.CompressionThresholdBytes = threshold;
        continue;
    }

    if (arg.StartsWith("--encrypt-key", StringComparison.OrdinalIgnoreCase))
    {
        security.EnableEncryption = true;
        var parts = arg.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
            security.EncryptionKeyBase64 = parts[1];
        else if (TryReadNext(args, ref i, out var value))
            security.EncryptionKeyBase64 = value;
        continue;
    }

    if (arg.Equals("--encrypt", StringComparison.OrdinalIgnoreCase))
    {
        security.EnableEncryption = true;
        continue;
    }
}

if (positional.Count > 0 && int.TryParse(positional[0], out var p))
    tcpPort = p;
if (positional.Count > 1 && int.TryParse(positional[1], out var wp))
    wsPort = wp;
if (positional.Count > 2 && !string.IsNullOrWhiteSpace(positional[2]))
    wsHost = positional[2];
if (positional.Count > 3 && int.TryParse(positional[3], out var kp))
    kcpPort = kp;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Game RPC Server TCP listening on 0.0.0.0:{tcpPort}. Press Ctrl+C to stop.");
Console.WriteLine($"Game RPC Server WS listening on ws://{wsHost}:{wsPort}/rpc.");
Console.WriteLine($"Game RPC Server KCP listening on 0.0.0.0:{kcpPort} (UDP).");

var tcpTask = RunTcpListenerAsync(tcpPort, cts.Token);
var wsTask = RunWebSocketListenerAsync(wsHost, wsPort, cts.Token);
var kcpTask = RunKcpListenerAsync(kcpPort, cts.Token);

try
{
    await Task.WhenAll(tcpTask, wsTask, kcpTask).ConfigureAwait(false);
}
finally
{
    Console.WriteLine("Server stopped.");
}

async Task RunTcpListenerAsync(int port, CancellationToken hostCt)
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

            var transport = new TcpServerTransport(client);
            _ = RunConnectionAsync(WrapSecurity(transport), client.Client.RemoteEndPoint?.ToString() ?? "?", hostCt);
        }
    }
    finally
    {
        listener.Stop();
    }
}

async Task RunWebSocketListenerAsync(string host, int port, CancellationToken hostCt)
{
    var listener = new HttpListener();
    listener.Prefixes.Add(BuildWsPrefix(host, port));
    listener.Start();

    using var reg = hostCt.Register(() =>
    {
        try
        {
            listener.Stop();
        }
        catch
        {
        }
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

async Task RunKcpListenerAsync(int port, CancellationToken hostCt)
{
    while (!hostCt.IsCancellationRequested)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, port));

        var buffer = new byte[64 * 1024];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        SocketReceiveFromResult res;
        try
        {
            res = await socket.ReceiveFromAsync(buffer, SocketFlags.None, remote, hostCt).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (ObjectDisposedException)
        {
            break;
        }

        if (res.ReceivedBytes < 4)
            continue;

        var conv = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(0, 4));
        var remoteEndPoint = res.RemoteEndPoint;
        var remoteText = remoteEndPoint?.ToString() ?? "?";
        Console.WriteLine($"[{remoteText}] KCP Connected (conv={conv}).");

        var transport = new KcpServerTransport(socket, remoteEndPoint!, conv, buffer.AsMemory(0, res.ReceivedBytes));
        await RunConnectionAsync(WrapSecurity(transport), remoteText, hostCt).ConfigureAwait(false);
    }
}

async Task HandleWebSocketClientAsync(HttpListenerContext ctx, CancellationToken hostCt)
{
    var remote = ctx.Request.RemoteEndPoint?.ToString() ?? "?";
    Console.WriteLine($"[{remote}] WS Connected.");

    try
    {
        var wsContext = await ctx.AcceptWebSocketAsync(null).ConfigureAwait(false);
        var transport = new WebSocketServerTransport(wsContext.WebSocket);
        await RunConnectionAsync(WrapSecurity(transport), remote, hostCt)
            .ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{remote}] WS Error: {ex.Message}");
        try
        {
            ctx.Response.StatusCode = 500;
            ctx.Response.Close();
        }
        catch
        {
        }
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

async Task RunConnectionAsync(ITransport transport, string remote, CancellationToken hostCt)
{
    RpcServer? server = null;

    try
    {
        server = new RpcServer(transport);

        PlayerServiceBinder.Bind(server, new PlayerServiceImpl());
        await server.StartAsync(hostCt).ConfigureAwait(false);
        await server.WaitForCompletionAsync().ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        // Host shutdown
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{remote}] Error: {ex}");
    }
    finally
    {
        if (server is not null)
            await server.StopAsync().ConfigureAwait(false);

        await transport.DisposeAsync().ConfigureAwait(false);
    }

    Console.WriteLine($"[{remote}] Disconnected.");
}

ITransport WrapSecurity(ITransport transport)
{
    if (!security.IsEnabled)
        return transport;

    return new TransformingTransport(transport, security);
}

static bool TryReadNext(string[] args, ref int index, out string value)
{
    var next = index + 1;
    if (next >= args.Length)
    {
        value = string.Empty;
        return false;
    }

    index = next;
    value = args[next];
    return true;
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

    public ValueTask PingAsync()
    {
        return default;
    }
}
