using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Rpc.Runtime
{
    public sealed class WebSocketTransport : ITransport
    {
        private readonly Uri _uri;
        private byte[] _accum = Array.Empty<byte>(); // simplistic accumulator
        private ClientWebSocket? _ws;

        public WebSocketTransport(Uri uri)
        {
            _uri = uri;
        }

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        public async ValueTask ConnectAsync(CancellationToken ct = default)
        {
            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(_uri, ct);
        }

        public async ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (_ws is null) throw new InvalidOperationException("Not connected.");
            var packed = LengthPrefix.Pack(frame.Span);
            await _ws.SendAsync(packed, WebSocketMessageType.Binary, true, ct);
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            if (_ws is null) throw new InvalidOperationException("Not connected.");

            while (true)
            {
                var tmp = ArrayPool<byte>.Shared.Rent(8 * 1024);
                try
                {
                    var res = await _ws.ReceiveAsync(tmp, ct);
                    if (res.MessageType == WebSocketMessageType.Close)
                        throw new IOException("WebSocket closed.");

                    // append
                    var oldLen = _accum.Length;
                    Array.Resize(ref _accum, oldLen + res.Count);
                    Array.Copy(tmp, 0, _accum, oldLen, res.Count);

                    // try unpack using ReadOnlySequence
                    var seq = new ReadOnlySequence<byte>(_accum);
                    if (LengthPrefix.TryUnpack(ref seq, out var payloadSeq))
                    {
                        var payload = payloadSeq.ToArray();
                        var remaining = seq.ToArray();
                        _accum = remaining;
                        return payload;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(tmp);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_ws is not null)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                }
                catch
                {
                }

                _ws.Dispose();
            }
        }
    }
}