using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets.Kcp;
using System.Collections.Concurrent;

namespace Game.Rpc.Runtime
{
    /// <summary>
    ///     ITransport implementation over KCP (UDP).
    ///     Uses the same length-prefix framing (4-byte big-endian + payload) as other transports.
    /// </summary>
    public sealed class KcpServerTransport : ITransport, IKcpCallback, IRentable
    {
        private const int MaxFrameSize = 64 * 1024 * 1024;
        private const int UpdateIntervalMs = 10;

        private readonly Socket _socket;
        private readonly EndPoint _remote;
        private readonly SimpleSegManager.Kcp _kcp;
        private readonly object _kcpGate = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly ReadOnlyMemory<byte> _initialPacket;
        private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _frames = new();

        private Task? _updateLoop;
        private bool _connected;
        private byte[] _accum = Array.Empty<byte>();

        public KcpServerTransport(Socket socket, EndPoint remote, uint conv, ReadOnlyMemory<byte> initialPacket)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
            _initialPacket = initialPacket;

            _kcp = new SimpleSegManager.Kcp(conv, this, this);
        }

        public bool IsConnected => _connected;

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            if (_connected)
                return default;

            _connected = true;
            _updateLoop = Task.Run(UpdateLoopAsync);

            if (!_initialPacket.IsEmpty)
                ProcessInput(_initialPacket.Span);

            return default;
        }

        public ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (!_connected)
                throw new InvalidOperationException("Not connected.");

            var packed = LengthPrefix.Pack(frame.Span);
            lock (_kcpGate)
            {
                _kcp.Send(packed, null);
                var now = DateTimeOffset.UtcNow;
                _kcp.Update(in now);
            }

            return default;
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            if (!_connected)
                throw new InvalidOperationException("Not connected.");

            var buffer = new byte[64 * 1024];
            EndPoint any = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                if (TryDequeueFrame(out var queued))
                    return queued;

                var res = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, any, ct).ConfigureAwait(false);
                if (!EndPointEquals(res.RemoteEndPoint, _remote))
                    continue;

                ProcessInput(buffer.AsSpan(0, res.ReceivedBytes));

                if (TryDequeueFrame(out var frame))
                    return frame;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _connected = false;
            _cts.Cancel();

            if (_updateLoop is not null)
            {
                try
                {
                    await _updateLoop.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            _cts.Dispose();
            _kcp.Dispose();

            try
            {
                _socket.Dispose();
            }
            catch
            {
            }

        }

        IMemoryOwner<byte> IRentable.RentBuffer(int size)
        {
            return MemoryPool<byte>.Shared.Rent(size);
        }

        void IKcpCallback.Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            try
            {
                var mem = buffer.Memory.Slice(0, avalidLength);
                _socket.SendTo(mem.Span, SocketFlags.None, _remote);
            }
            finally
            {
                buffer.Dispose();
            }
        }

        private void ProcessInput(ReadOnlySpan<byte> data)
        {
            lock (_kcpGate)
            {
                _kcp.Input(data);
                DrainKcp();
            }
        }

        private void DrainKcp()
        {
            while (true)
            {
                var size = _kcp.PeekSize();
                if (size <= 0)
                    break;

                if (size > MaxFrameSize)
                    throw new InvalidOperationException($"Frame too large: {size} bytes");

                var buf = new byte[size];
                _kcp.Recv(buf);
                AppendAndUnpack(buf);
            }
        }

        private void AppendAndUnpack(ReadOnlySpan<byte> payload)
        {
            var oldLen = _accum.Length;
            Array.Resize(ref _accum, oldLen + payload.Length);
            payload.CopyTo(_accum.AsSpan(oldLen));

            while (true)
            {
                var seq = new ReadOnlySequence<byte>(_accum);
                if (!LengthPrefix.TryUnpack(ref seq, out var payloadSeq))
                    break;

                var frame = payloadSeq.ToArray();
                if (frame.Length > 0)
                    EnqueueFrame(frame);

                _accum = seq.ToArray();
            }
        }

        private bool TryDequeueFrame(out ReadOnlyMemory<byte> frame)
        {
            return _frames.TryDequeue(out frame);
        }

        private void EnqueueFrame(ReadOnlyMemory<byte> frame)
        {
            _frames.Enqueue(frame);
        }

        private async Task UpdateLoopAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    lock (_kcpGate)
                    {
                        var now = DateTimeOffset.UtcNow;
                        _kcp.Update(in now);
                    }

                    await Task.Delay(UpdateIntervalMs, _cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static bool EndPointEquals(EndPoint? a, EndPoint? b)
        {
            return a is not null && b is not null && a.Equals(b);
        }
    }
}
