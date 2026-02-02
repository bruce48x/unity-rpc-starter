using System;

namespace ULinkRPC.Runtime
{
    public sealed class TransportSecurityConfig
    {
        public bool EnableCompression;
        public int CompressionThresholdBytes = 1024;
        public bool EnableEncryption;

        public byte[]? EncryptionKey;
        public string? EncryptionKeyBase64;

        public bool IsEnabled => EnableCompression || EnableEncryption;

        public byte[]? ResolveKey()
        {
            if (EncryptionKey is { Length: > 0 })
                return EncryptionKey;

            if (!string.IsNullOrWhiteSpace(EncryptionKeyBase64))
                return Convert.FromBase64String(EncryptionKeyBase64);

            return null;
        }
    }
}
