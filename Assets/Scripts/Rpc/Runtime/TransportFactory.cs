using System;

namespace Game.Rpc.Runtime
{
    public enum TransportKind
    {
        Loopback,
        Tcp,
        WebSocket,
        Kcp
    }

    public sealed class TransportConfig
    {
        public string Host = "127.0.0.1";
        public TransportKind Kind = TransportKind.Loopback;
        public int Port = 20000;

        // ws
        public string WsUrl = "ws://127.0.0.1:20001/rpc";
    }

    public static class TransportFactory
    {
        public static ITransport Create(TransportConfig cfg, out ITransport? pairedServerForLoopback)
        {
            pairedServerForLoopback = null;

            return cfg.Kind switch
            {
                TransportKind.Loopback => CreateLoopback(out pairedServerForLoopback),
                TransportKind.Tcp => new TcpTransport(cfg.Host, cfg.Port),
                TransportKind.WebSocket => new WebSocketTransport(new Uri(cfg.WsUrl)),
                TransportKind.Kcp => new KcpTransport(cfg.Host, cfg.Port),
                _ => throw new ArgumentOutOfRangeException(nameof(cfg.Kind))
            };
        }

        private static ITransport CreateLoopback(out ITransport pairedServer)
        {
            var (c, s) = LoopbackTransport.CreatePair();
            pairedServer = s;
            return c;
        }
    }
}