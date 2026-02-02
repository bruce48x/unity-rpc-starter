using System;

namespace ULinkRPC.Runtime
{
    public interface IRpcSerializer
    {
        byte[] Serialize<T>(T value);
        T Deserialize<T>(ReadOnlySpan<byte> data);
        T Deserialize<T>(ReadOnlyMemory<byte> data);
    }
}
