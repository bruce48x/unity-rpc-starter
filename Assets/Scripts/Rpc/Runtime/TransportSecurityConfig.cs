using System;

namespace Game.Rpc.Runtime
{
    public sealed class TransportSecurityConfig
    {
        public bool EnableCompression;
        public int CompressionThresholdBytes = 1024;
        public bool EnableEncryption;

        // Optional: either set raw key bytes or a base64 string (must be 16/24/32 bytes after decoding)
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
