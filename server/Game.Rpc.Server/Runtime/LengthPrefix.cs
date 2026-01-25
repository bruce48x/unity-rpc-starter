using System;

namespace Game.Rpc.Runtime
{
    /// <summary>
    ///     Network framing: uint32 length prefix (big-endian) + payload bytes.
    ///     Matches Unity client's LengthPrefix for wire compatibility.
    /// </summary>
    public static class LengthPrefix
    {
        public static byte[] Pack(ReadOnlySpan<byte> payload)
        {
            var buf = new byte[4 + payload.Length];
            var len = (uint)payload.Length;
            buf[0] = (byte)(len >> 24);
            buf[1] = (byte)(len >> 16);
            buf[2] = (byte)(len >> 8);
            buf[3] = (byte)len;
            payload.CopyTo(buf.AsSpan(4));
            return buf;
        }
    }
}
