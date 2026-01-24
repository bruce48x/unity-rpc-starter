using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MemoryPack;

namespace Game.Rpc.Runtime
{
    public sealed class RpcClient : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<uint, TaskCompletionSource<RpcResponseEnvelope>> _pending = new();
        private readonly ITransport _transport;
        private uint _nextId = 1;

        private Task? _recvLoop;

        public RpcClient(ITransport transport)
        {
            _transport = transport;
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            if (_recvLoop is not null)
                try
                {
                    await _recvLoop.ConfigureAwait(false);
                }
                catch
                {
                }

            await _transport.DisposeAsync().ConfigureAwait(false);
            _cts.Dispose();
        }

        public async ValueTask StartAsync(CancellationToken ct = default)
        {
            await _transport.ConnectAsync(ct);
            _recvLoop = Task.Run(ReceiveLoopAsync);
        }

        public async ValueTask<TResult> CallAsync<TArg, TResult>(int serviceId, int methodId, TArg? arg,
            CancellationToken ct = default)
        {
            var id = unchecked(_nextId++);
            var tcs = new TaskCompletionSource<RpcResponseEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = tcs;

            try
            {
                var argBytes = arg is null ? Array.Empty<byte>() : MemoryPackSerializer.Serialize(arg);

                var req = new RpcRequestEnvelope
                {
                    RequestId = id,
                    ServiceId = serviceId,
                    MethodId = methodId,
                    Payload = argBytes
                };

                var reqBytes = MemoryPackSerializer.Serialize(req);
                await _transport.SendFrameAsync(reqBytes, ct);

                using var reg = ct.Register(() =>
                {
                    if (_pending.TryRemove(id, out var p))
                        p.TrySetCanceled(ct);
                });

                var resp = await tcs.Task.ConfigureAwait(false);
                if (resp.Status != RpcStatus.Ok)
                    throw new InvalidOperationException($"RPC failed: {resp.Status}, {resp.ErrorMessage}");

                if (typeof(TResult) == typeof(RpcVoid))
                    return (TResult)(object)RpcVoid.Instance;

                return MemoryPackSerializer.Deserialize<TResult>(resp.Payload)!;
            }
            finally
            {
                _pending.TryRemove(id, out _);
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var ct = _cts.Token;
            while (!ct.IsCancellationRequested)
            {
                var frame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
                var resp = MemoryPackSerializer.Deserialize<RpcResponseEnvelope>(frame.Span)!;

                if (_pending.TryRemove(resp.RequestId, out var tcs))
                    tcs.TrySetResult(resp);
            }
        }
    }
}