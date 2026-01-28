using System;
using MemoryPack;

namespace Game.Rpc.Runtime
{
    public sealed class MemoryPackRpcSerializer : IRpcSerializer
    {
        public byte[] Serialize<T>(T value)
        {
            return MemoryPackSerializer.Serialize(value);
        }

        public T Deserialize<T>(ReadOnlySpan<byte> data)
        {
            return MemoryPackSerializer.Deserialize<T>(data)!;
        }

        public T Deserialize<T>(ReadOnlyMemory<byte> data)
        {
            return MemoryPackSerializer.Deserialize<T>(data.Span)!;
        }
    }
}
