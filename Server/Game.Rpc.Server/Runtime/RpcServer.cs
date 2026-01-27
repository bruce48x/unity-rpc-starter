using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemoryPack;

namespace Game.Rpc.Runtime
{
    public delegate ValueTask<RpcResponseEnvelope> RpcHandler(RpcRequestEnvelope req, CancellationToken ct);

    public sealed class RpcServer
    {
        private readonly Dictionary<(int serviceId, int methodId), RpcHandler> _handlers = new();
        private readonly ITransport _transport;

        private CancellationTokenSource? _cts;
        private Task? _loop;

        public RpcServer(ITransport transport)
        {
            _transport = transport;
        }

        public void Register(int serviceId, int methodId, RpcHandler handler)
        {
            _handlers[(serviceId, methodId)] = handler;
        }

        public async ValueTask StartAsync(CancellationToken ct = default)
        {
            await _transport.ConnectAsync(ct);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _loop = Task.Run(LoopAsync);
        }

        private async Task LoopAsync()
        {
            if (_cts is null) return;
            var ct = _cts.Token;

            while (!ct.IsCancellationRequested)
            {
                ReadOnlyMemory<byte> frame;
                try
                {
                    frame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (InvalidOperationException) when (!_transport.IsConnected)
                {
                    break;
                }

                if (frame.Length == 0)
                    break;

                var req = MemoryPackSerializer.Deserialize<RpcRequestEnvelope>(frame.Span)!;

                RpcResponseEnvelope resp;
                if (_handlers.TryGetValue((req.ServiceId, req.MethodId), out var handler))
                    try
                    {
                        resp = await handler(req, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        resp = new RpcResponseEnvelope
                        {
                            RequestId = req.RequestId,
                            Status = RpcStatus.Exception,
                            Payload = Array.Empty<byte>(),
                            ErrorMessage = ex.ToString()
                        };
                    }
                else
                    resp = new RpcResponseEnvelope
                    {
                        RequestId = req.RequestId,
                        Status = RpcStatus.NotFound,
                        Payload = Array.Empty<byte>(),
                        ErrorMessage = $"No handler for {req.ServiceId}:{req.MethodId}"
                    };

                var respBytes = MemoryPackSerializer.Serialize(resp);
                await _transport.SendFrameAsync(respBytes, ct).ConfigureAwait(false);
            }
        }

        public async ValueTask StopAsync()
        {
            if (_cts is null) return;
            _cts.Cancel();
            if (_loop is not null)
                try
                {
                    await _loop.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }

            _cts.Dispose();
            _cts = null;
        }
    }
}
