namespace ULinkRPC.Runtime
{
    public sealed class TransportConfig
    {
        public TransportKind Kind { get; set; } = TransportKind.Tcp;
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 20000;
        public string WsUrl { get; set; } = "ws://127.0.0.1:20001/rpc";
    }
}
