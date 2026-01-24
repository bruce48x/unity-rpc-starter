using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Rpc.Runtime
{
    public sealed class TcpTransport : ITransport
    {
        private readonly string _host;
        private readonly int _port;

        private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _recvQueue = new();
        private readonly SemaphoreSlim _recvSignal = new(0);

        // Accumulator for stream reassembly
        private byte[] _accum = new byte[64 * 1024];
        private int _accumLen;

        private TcpClient? _client;

        private CancellationTokenSource? _cts;
        private Task? _recvLoop;
        private NetworkStream? _stream;

        public TcpTransport(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public bool IsConnected => _client?.Connected == true;

        public async ValueTask ConnectAsync(CancellationToken ct = default)
        {
            if (_client is not null) throw new InvalidOperationException("Already connected.");

            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port);
            _stream = _client.GetStream();

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _recvLoop = Task.Run(() => RecvLoopAsync(_cts.Token));
        }

        public async ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (_stream is null) throw new InvalidOperationException("Not connected.");

            // pack: 4-byte big-endian length prefix + payload
            var packed = LengthPrefix.Pack(frame.Span);
            await _stream.WriteAsync(packed, 0, packed.Length, ct).ConfigureAwait(false);
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            // Wait for at least one frame
            await _recvSignal.WaitAsync(ct).ConfigureAwait(false);

            if (_recvQueue.TryDequeue(out var data))
                return data;

            // Rare race: signal without data
            throw new InvalidOperationException("Receive signal without queued frame.");
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
            }

            if (_recvLoop is not null)
                try
                {
                    await _recvLoop.ConfigureAwait(false);
                }
                catch
                {
                }

            _cts?.Dispose();
            _cts = null;

            try
            {
                _stream?.Dispose();
            }
            catch
            {
            }

            try
            {
                _client?.Dispose();
            }
            catch
            {
            }

            _stream = null;
            _client = null;

            // Do not dispose _recvSignal: in-flight awaits may still observe it.
        }

        private async Task RecvLoopAsync(CancellationToken ct)
        {
            Exception? err = null;

            try
            {
                var buf = new byte[16 * 1024];

                while (!ct.IsCancellationRequested)
                {
                    if (_stream is null) break;

                    var n = await _stream.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false);
                    if (n <= 0) throw new IOException("Remote closed.");

                    AppendToAccum(buf, n);
                    DrainFramesFromAccum();
                }
            }
            catch (Exception ex)
            {
                err = ex;
            }
            finally
            {
                // Unblock any waiters with a terminal signal.
                // We enqueue an empty frame to indicate closure if needed.
                _recvQueue.Enqueue(ReadOnlyMemory<byte>.Empty);
                _recvSignal.Release();

                // Close socket
                try
                {
                    _stream?.Dispose();
                }
                catch
                {
                }

                try
                {
                    _client?.Close();
                }
                catch
                {
                }
            }
        }

        private void AppendToAccum(byte[] src, int count)
        {
            EnsureAccumCapacity(_accumLen + count);
            Buffer.BlockCopy(src, 0, _accum, _accumLen, count);
            _accumLen += count;
        }

        private void DrainFramesFromAccum()
        {
            // We parse frames from _accum: [len(4)][payload(len)]...
            var offset = 0;

            while (true)
            {
                if (_accumLen - offset < 4) break;

                var len =
                    ((uint)_accum[offset] << 24) |
                    ((uint)_accum[offset + 1] << 16) |
                    ((uint)_accum[offset + 2] << 8) |
                    _accum[offset + 3];

                if (len > 64 * 1024 * 1024)
                    throw new InvalidOperationException($"Frame too large: {len} bytes");

                if (_accumLen - offset < 4 + (int)len) break;

                var payload = new byte[len];
                Buffer.BlockCopy(_accum, offset + 4, payload, 0, (int)len);

                _recvQueue.Enqueue(payload);
                _recvSignal.Release();

                offset += 4 + (int)len;
            }

            if (offset == 0) return;

            // shift remaining bytes to start
            var remaining = _accumLen - offset;
            if (remaining > 0)
                Buffer.BlockCopy(_accum, offset, _accum, 0, remaining);

            _accumLen = remaining;
        }

        private void EnsureAccumCapacity(int needed)
        {
            if (_accum.Length >= needed) return;

            var newCap = _accum.Length;
            while (newCap < needed) newCap *= 2;

            Array.Resize(ref _accum, newCap);
        }
    }
}