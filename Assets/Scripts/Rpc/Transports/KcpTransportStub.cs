using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Game.Rpc.Runtime
{
    /// <summary>
    /// KCP stub transport used to keep the abstraction stable until real KCP integration is plugged in.
    /// It behaves like a loopback queue and can later be replaced by a real KCP implementation without changing RPC code.
    /// </summary>
    public sealed class KcpTransportStub : ITransport
    {
        private readonly Channel<ReadOnlyMemory<byte>> _in = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        public bool IsConnected { get; private set; }

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected.");
            // For now: echo back for testing, so client alone can run.
            _in.Writer.TryWrite(frame.ToArray());
            return ValueTask.CompletedTask;
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected.");
            return await _in.Reader.ReadAsync(ct);
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
