using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace ULinkRPC.Runtime
{
    internal sealed class LoopbackTransport : ITransport
    {
        private readonly LoopbackQueue _incoming;
        private readonly LoopbackQueue _outgoing;
        private bool _connected;

        private LoopbackTransport(LoopbackQueue incoming, LoopbackQueue outgoing)
        {
            _incoming = incoming;
            _outgoing = outgoing;
        }

        public static void CreatePair(out ITransport client, out ITransport server)
        {
            var aToB = new LoopbackQueue();
            var bToA = new LoopbackQueue();
            client = new LoopbackTransport(bToA, aToB);
            server = new LoopbackTransport(aToB, bToA);
        }

        public bool IsConnected => _connected;

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            _connected = true;
            return default;
        }

        public async ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (!_connected)
                throw new InvalidOperationException("Not connected.");

            await _outgoing.WriteAsync(frame.ToArray(), ct).ConfigureAwait(false);
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            if (!_connected)
                throw new InvalidOperationException("Not connected.");

            return await _incoming.ReadAsync(ct).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            _connected = false;
            _outgoing.Complete();
            return default;
        }

        private sealed class LoopbackQueue
        {
            private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _queue = new();
            private readonly SemaphoreSlim _signal = new(0);
            private bool _completed;

            public ValueTask WriteAsync(ReadOnlyMemory<byte> item, CancellationToken ct)
            {
                if (_completed)
                    throw new InvalidOperationException("Loopback queue is completed.");

                ct.ThrowIfCancellationRequested();
                _queue.Enqueue(item);
                _signal.Release();
                return default;
            }

            public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct)
            {
                while (true)
                {
                    if (_queue.TryDequeue(out var item))
                        return item;

                    if (_completed)
                        return ReadOnlyMemory<byte>.Empty;

                    await _signal.WaitAsync(ct).ConfigureAwait(false);
                }
            }

            public void Complete()
            {
                _completed = true;
                _signal.Release();
            }
        }
    }
}
