using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Rpc.Runtime
{
    /// <summary>
    ///     Transport boundary for RPC: it sends and receives COMPLETE FRAMES (one message).
    ///     TCP/WS/KCP differences are hidden below this interface.
    /// </summary>
    public interface ITransport : IAsyncDisposable
    {
        bool IsConnected { get; }
        ValueTask ConnectAsync(CancellationToken ct = default);
        ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default);
        ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default);
    }
}