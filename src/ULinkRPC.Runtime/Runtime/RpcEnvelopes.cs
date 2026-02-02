using MemoryPack;

namespace ULinkRPC.Runtime
{
    public enum RpcStatus : byte
    {
        Ok = 0,
        NotFound = 1,
        Exception = 2
    }

    [MemoryPackable]
    public partial class RpcRequestEnvelope
    {
        public uint RequestId { get; set; }
        public int ServiceId { get; set; }
        public int MethodId { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }

    [MemoryPackable]
    public partial class RpcResponseEnvelope
    {
        public uint RequestId { get; set; }
        public RpcStatus Status { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();
        public string? ErrorMessage { get; set; }
    }

    [MemoryPackable]
    public partial class RpcVoid
    {
        public static readonly RpcVoid Instance = new();
    }
}
