using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Rpc.Runtime
{
    public sealed class RpcClient : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<uint, TaskCompletionSource<RpcResponseEnvelope>> _pending = new();
        private readonly ITransport _transport;
        private readonly IRpcSerializer _serializer;
        private uint _nextId = 1;

        private Task? _recvLoop;

        public RpcClient(ITransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serializer = new MemoryPackRpcSerializer();
        }

        public RpcClient(ITransport transport, IRpcSerializer serializer)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
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

        public event Action<Exception?>? Disconnected;

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
                var argBytes = arg is null ? Array.Empty<byte>() : _serializer.Serialize(arg);

                var req = new RpcRequestEnvelope
                {
                    RequestId = id,
                    ServiceId = serviceId,
                    MethodId = methodId,
                    Payload = argBytes
                };

                var reqBytes = _serializer.Serialize(req);
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

                return _serializer.Deserialize<TResult>(resp.Payload.AsSpan())!;
            }
            finally
            {
                _pending.TryRemove(id, out _);
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var ct = _cts.Token;
            Exception? err = null;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var frame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
                    if (frame.IsEmpty)
                        throw new InvalidOperationException("Transport closed.");

                    var resp = _serializer.Deserialize<RpcResponseEnvelope>(frame.Span)!;

                    if (_pending.TryRemove(resp.RequestId, out var tcs))
                        tcs.TrySetResult(resp);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    err = ex;
            }
            finally
            {
                if (err is not null)
                    FailAllPending(err);

                Disconnected?.Invoke(err);
            }
        }

        private void FailAllPending(Exception ex)
        {
            foreach (var item in _pending)
                if (_pending.TryRemove(item.Key, out var tcs))
                    tcs.TrySetException(ex);
        }
    }
}