using System;

namespace ULinkRPC.Runtime
{
    public static class TransportFactory
    {
        public static ITransport Create(TransportConfig config, out ITransport? serverTransport)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));

            serverTransport = null;

            switch (config.Kind)
            {
                case TransportKind.Loopback:
                    LoopbackTransport.CreatePair(out var client, out var server);
                    serverTransport = server;
                    return client;

                case TransportKind.Tcp:
                    return new TcpTransport(config.Host, config.Port);

                case TransportKind.WebSocket:
                    throw new NotSupportedException("WebSocket transport is not implemented in ULinkRPC.Runtime.Client.");

                case TransportKind.Kcp:
                    throw new NotSupportedException("Kcp transport is not implemented in ULinkRPC.Runtime.Client.");

                default:
                    throw new ArgumentOutOfRangeException(nameof(config.Kind), config.Kind, "Unsupported transport kind.");
            }
        }
    }
}
