using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Rpc.Runtime
{
    /// <summary>
    ///     KCP stub transport used to keep the abstraction stable until real KCP integration is plugged in.
    ///     Behaves like a simple async queue.
    /// </summary>
    public sealed class KcpTransportStub : ITransport
    {
        private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);

        public bool IsConnected { get; private set; }

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            IsConnected = true;
            return default;
        }

        public ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected.");

            // echo back for stub behavior
            _queue.Enqueue(frame.ToArray());
            _signal.Release();
            return default;
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected.");

            await _signal.WaitAsync(ct).ConfigureAwait(false);

            if (_queue.TryDequeue(out var data))
                return data;

            // theoretically impossible, but keep safe
            throw new InvalidOperationException("Signal without data.");
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            _signal.Dispose();
            return default;
        }
    }
}