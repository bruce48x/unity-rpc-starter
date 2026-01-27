using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using Game.Rpc.Runtime.Generated;
using UnityEngine;

namespace Game.Rpc.Runtime
{
    public sealed class RpcConnectionTester : MonoBehaviour
    {
        public enum ConnectionState
        {
            Idle,
            Connecting,
            Connected,
            Disconnected,
            Error
        }

        public event Action<ConnectionState, string?>? StatusChanged;

        [Header("Transport")]
        public TransportKind Kind = TransportKind.Tcp;
        public string Host = "127.0.0.1";
        public int Port = 20000;
        public string WsUrl = "ws://127.0.0.1:20001/rpc";

        [Header("Login")]
        public string Account = "a";
        public string Password = "b";

        public bool AutoConnect = true;

        private RpcClient? _client;
        private IPlayerServiceClient? _proxy;
        private CancellationTokenSource? _cts;
        private ConnectionState _state = ConnectionState.Idle;
        private bool _disconnectedDuringConnect;

        private async void Start()
        {
            if (!AutoConnect)
                return;

            await ConnectAndTestAsync();
        }

        public async Task ConnectAndTestAsync()
        {
            if (_client is not null)
            {
                Debug.LogWarning("RpcConnectionTester already connected.");
                return;
            }

            try
            {
                UpdateStatus(ConnectionState.Connecting, "Connecting...");
                _cts = new CancellationTokenSource();
                var cfg = new TransportConfig
                {
                    Kind = Kind,
                    Host = Host,
                    Port = Port,
                    WsUrl = WsUrl
                };

                var transport = TransportFactory.Create(cfg, out _);
                _client = new RpcClient(transport);
                _client.Disconnected += OnClientDisconnected;
                await _client.StartAsync(_cts.Token);
                UpdateStatus(ConnectionState.Connected, "Connected");

                if (_disconnectedDuringConnect)
                    throw new InvalidOperationException("Connection closed.");

                _proxy = new IPlayerServiceClient(_client);

                var reply = await _proxy.LoginAsync(new LoginRequest
                {
                    Account = Account,
                    Password = Password
                });

                Debug.Log($"Login ok: code={reply.Code}, token={reply.Token}");

                await _proxy.PingAsync();
                Debug.Log("Ping ok.");

                if (_disconnectedDuringConnect)
                {
                    _disconnectedDuringConnect = false;
                    await CleanupAsync(false);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"RPC test failed: {ex}");
                UpdateStatus(ConnectionState.Error, ex.Message);
                await CleanupAsync(false);
            }
        }

        public void ConnectFromUi()
        {
            _ = ConnectAndTestAsync();
        }

        private async void OnDestroy()
        {
            await CleanupAsync(true);
        }

        private async Task CleanupAsync(bool notifyDisconnected)
        {
            if (_client is null)
                return;

            try
            {
                _client.Disconnected -= OnClientDisconnected;
                await _client.DisposeAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"RPC cleanup error: {ex}");
            }
            finally
            {
                _client = null;
                _proxy = null;
                if (_cts is not null)
                {
                    _cts.Dispose();
                    _cts = null;
                }
            }

            if (notifyDisconnected)
                UpdateStatus(ConnectionState.Disconnected, "Disconnected");
        }

        private void OnClientDisconnected(Exception? ex)
        {
            var wasConnecting = _state == ConnectionState.Connecting;
            if (ex is null)
                UpdateStatus(ConnectionState.Disconnected, "Disconnected");
            else
                UpdateStatus(ConnectionState.Error, ex.Message);

            if (wasConnecting)
                _disconnectedDuringConnect = true;
            else
                _ = CleanupAsync(false);
        }

        private void UpdateStatus(ConnectionState state, string? message)
        {
            _state = state;
            StatusChanged?.Invoke(state, message);
        }
    }
}
