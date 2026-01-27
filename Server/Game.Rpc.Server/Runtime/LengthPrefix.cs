using System.Buffers;

namespace Game.Rpc.Runtime;

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

    public static bool TryUnpack(ref ReadOnlySequence<byte> seq, out ReadOnlySequence<byte> payload)
    {
        payload = default;
        if (seq.Length < 4) return false;

        Span<byte> hdr = stackalloc byte[4];
        seq.Slice(0, 4).CopyTo(hdr);
        var len = ((uint)hdr[0] << 24) | ((uint)hdr[1] << 16) | ((uint)hdr[2] << 8) | hdr[3];

        if (seq.Length < 4 + len) return false;

        payload = seq.Slice(4, len);
        seq = seq.Slice(4 + len);
        return true;
    }
}