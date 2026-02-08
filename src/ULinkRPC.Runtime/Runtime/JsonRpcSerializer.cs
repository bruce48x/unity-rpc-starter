using System;
using System.Text.Json;

namespace ULinkRPC.Runtime
{
    public sealed class JsonRpcSerializer : IRpcSerializer
    {
        private readonly JsonSerializerOptions _options;

        public JsonRpcSerializer(JsonSerializerOptions? options = null)
        {
            _options = options is null
                ? new JsonSerializerOptions()
                : new JsonSerializerOptions(options);

            // Required for ValueTuple payloads used by multi-parameter RPC methods.
            _options.IncludeFields = true;
        }

        public byte[] Serialize<T>(T value)
        {
            return JsonSerializer.SerializeToUtf8Bytes(value, _options);
        }

        public T Deserialize<T>(ReadOnlySpan<byte> data)
        {
            return JsonSerializer.Deserialize<T>(data, _options)!;
        }

        public T Deserialize<T>(ReadOnlyMemory<byte> data)
        {
            return JsonSerializer.Deserialize<T>(data.Span, _options)!;
        }
    }
}
