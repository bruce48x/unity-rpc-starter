using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets.Kcp;

namespace Game.Rpc.Runtime
{
    /// <summary>
    ///     KCP transport (client side) over UDP.
    ///     Uses the same length-prefix framing (4-byte big-endian + payload) as other transports.
    /// </summary>
    public sealed class KcpTransport : ITransport, IKcpCallback, IRentable
    {
        private const int MaxFrameSize = 64 * 1024 * 1024;
        private const int UpdateIntervalMs = 10;

        private readonly string _host;
        private readonly int _port;
        private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _frames = new();

        private readonly object _kcpGate = new();
        private CancellationTokenSource? _cts;
        private Socket? _socket;
        private EndPoint? _remote;
        private SimpleSegManager.Kcp? _kcp;
        private Task? _updateLoop;
        private bool _connected;
        private byte[] _accum = Array.Empty<byte>();

        public KcpTransport(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public bool IsConnected => _connected;

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            if (_connected)
                return default;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Connect(_host, _port);
            _remote = _socket.RemoteEndPoint ?? new IPEndPoint(IPAddress.Loopback, _port);

            var conv = (uint)Environment.TickCount;
            _kcp = new SimpleSegManager.Kcp(conv, this, this);
            _connected = true;

            _updateLoop = Task.Run(UpdateLoopAsync);
            return default;
        }

        public ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (!_connected || _kcp is null)
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
            if (!_connected || _socket is null || _kcp is null)
                throw new InvalidOperationException("Not connected.");

            var buffer = new byte[64 * 1024];

            while (true)
            {
                if (_frames.TryDequeue(out var queued))
                    return queued;

                var received = await _socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, ct).ConfigureAwait(false);
                if (received <= 0)
                    throw new InvalidOperationException("Transport closed.");

                ProcessInput(buffer.AsSpan(0, received));

                if (_frames.TryDequeue(out var frame))
                    return frame;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _connected = false;

            if (_cts is not null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

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

            _kcp?.Dispose();
            _kcp = null;

            try
            {
                _socket?.Dispose();
            }
            catch
            {
            }

            _socket = null;
            _remote = null;
        }

        IMemoryOwner<byte> IRentable.RentBuffer(int size)
        {
            return MemoryPool<byte>.Shared.Rent(size);
        }

        void IKcpCallback.Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            try
            {
                if (_socket is null)
                    return;

                var mem = buffer.Memory.Slice(0, avalidLength);
                _socket.Send(mem.Span, SocketFlags.None);
            }
            finally
            {
                buffer.Dispose();
            }
        }

        private void ProcessInput(ReadOnlySpan<byte> data)
        {
            if (_kcp is null)
                return;

            lock (_kcpGate)
            {
                _kcp.Input(data);
                DrainKcp();
            }
        }

        private void DrainKcp()
        {
            if (_kcp is null)
                return;

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
                    _frames.Enqueue(frame);

                _accum = seq.ToArray();
            }
        }

        private async Task UpdateLoopAsync()
        {
            if (_kcp is null || _cts is null)
                return;

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
    }
}
