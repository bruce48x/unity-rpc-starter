using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Rpc.Runtime
{
    /// <summary>
    /// In-memory transport for smoke tests. Two endpoints are paired.
    /// Uses ConcurrentQueue + SemaphoreSlim (Unity/IL2CPP friendly).
    /// </summary>
    public sealed class LoopbackTransport : ITransport
    {
        private sealed class Endpoint
        {
            public readonly ConcurrentQueue<ReadOnlyMemory<byte>> Queue = new();
            public readonly SemaphoreSlim Signal = new(0);
            public volatile bool Closed;
        }

        private readonly Endpoint _in;
        private readonly Endpoint _out;

        public bool IsConnected { get; private set; }

        private LoopbackTransport(Endpoint incoming, Endpoint outgoing)
        {
            _in = incoming;
            _out = outgoing;
        }

        public static (LoopbackTransport client, LoopbackTransport server) CreatePair()
        {
            var a = new Endpoint();
            var b = new Endpoint();
            return (new LoopbackTransport(a, b), new LoopbackTransport(b, a));
        }

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            if (_in.Closed || _out.Closed)
                throw new InvalidOperationException("Loopback endpoint already closed.");

            IsConnected = true;
            return default;
        }

        public ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected.");
            if (_out.Closed) throw new InvalidOperationException("Peer closed.");

            // Copy to avoid lifetime/ownership issues.
            _out.Queue.Enqueue(frame.ToArray());
            _out.Signal.Release();
            return default;
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected.");

            while (true)
            {
                await _in.Signal.WaitAsync(ct).ConfigureAwait(false);

                if (_in.Queue.TryDequeue(out var data))
                    return data;

                // Rare race: signal acquired but another dequeue won; loop again.
            }
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            _in.Closed = true;
            _out.Closed = true;

            // Do not dispose Signal here: both endpoints share objects and disposal order is ambiguous in tests.
            // For editor/test use this is acceptable. If you want strict disposal, add a ref-count.

            return default;
        }
    }
}
