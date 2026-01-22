using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Game.Rpc.Runtime
{
    /// <summary>
    /// In-memory transport for smoke tests. Two endpoints are paired.
    /// </summary>
    public sealed class LoopbackTransport : ITransport
    {
        private readonly Channel<ReadOnlyMemory<byte>> _in;
        private readonly Channel<ReadOnlyMemory<byte>> _out;

        public bool IsConnected { get; private set; }

        private LoopbackTransport(Channel<ReadOnlyMemory<byte>> incoming, Channel<ReadOnlyMemory<byte>> outgoing)
        {
            _in = incoming;
            _out = outgoing;
        }

        public static (LoopbackTransport client, LoopbackTransport server) CreatePair()
        {
            var a = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
            var b = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
            return (new LoopbackTransport(a, b), new LoopbackTransport(b, a));
        }

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected.");
            // copy to keep ownership simple
            var copy = frame.ToArray();
            _out.Writer.TryWrite(copy);
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
