using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Rpc.Runtime
{
    public sealed class TcpTransport : ITransport
    {
        private readonly string _host;
        private readonly int _port;

        private TcpClient? _client;
        private NetworkStream? _stream;
        private PipeReader? _reader;
        private PipeWriter? _writer;

        public bool IsConnected => _client?.Connected == true;

        public TcpTransport(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async ValueTask ConnectAsync(CancellationToken ct = default)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port, ct);
            _stream = _client.GetStream();
            _reader = PipeReader.Create(_stream);
            _writer = PipeWriter.Create(_stream);
        }

        public async ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            if (_writer is null) throw new InvalidOperationException("Not connected.");
            var packed = LengthPrefix.Pack(frame.Span);
            _writer.Write(packed);
            var res = await _writer.FlushAsync(ct);
            if (res.IsCompleted) { /* ok */ }
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            if (_reader is null) throw new InvalidOperationException("Not connected.");

            while (true)
            {
                var rr = await _reader.ReadAsync(ct);
                var buf = rr.Buffer;

                if (LengthPrefix.TryUnpack(ref buf, out var payloadSeq))
                {
                    byte[] payload = payloadSeq.ToArray();
                    _reader.AdvanceTo(buf.Start, buf.End);
                    return payload;
                }

                _reader.AdvanceTo(buf.Start, buf.End);

                if (rr.IsCompleted)
                    throw new IOException("Remote closed.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_reader is not null) await _reader.CompleteAsync();
            if (_writer is not null) await _writer.CompleteAsync();
            _stream?.Dispose();
            _client?.Dispose();
        }
    }
}
