using System;
using System.Threading.Tasks;
using Riptide;
using Riptide.Transports.Udp;

namespace Rip2p.Clients
{
    public class RiptideClient : BaseClient
    {
        private TaskCompletionSource<(bool success, string message)> _connectCompletionSource;

        private Client _client;

        private void Awake()
        {
            // TODO: Change UdpClient to steam client:
            // TODO: https://github.com/RiptideNetworking/SteamTransport
            
            var transport = new UdpClient();
            _client = new Client(transport);
            
            _client.Connected += OnConnected;
            _client.ClientConnected += OnOtherClientConnected;
            _client.ConnectionFailed += OnConnectionFailed;
            _client.Disconnected += OnDisconnected;
            _client.ClientDisconnected += OnOtherClientDisconnected;
            _client.MessageReceived += OnMessageReceived;
        }

        public override ushort Id => _client.Id;
        
        protected override Task<(bool success, string message)> ConnectInternalAsync(
            string address, 
            ushort port)
        {
            if (!_client.Connect($"{address}:{port}"))
            {
                return Task.FromResult((false, $"Can't connect to {address}:{port}"));
            }
            
            _connectCompletionSource = new TaskCompletionSource<(bool success, string message)>();
            return _connectCompletionSource.Task;
        }
        
        public override void Tick()
        {
            _client.Update();
        }
        
        private void OnConnected(object sender, EventArgs e)
        {
            _connectCompletionSource.TrySetResult((true, ""));
            OnClientConnected(_client.Id);
        }
        
        private void OnOtherClientConnected(object sender, ClientConnectedEventArgs e)
        {
            OnOtherClientConnected(e.Id);
        }
        
        private void OnConnectionFailed(object sender, ConnectionFailedEventArgs e)
        {
            _connectCompletionSource.TrySetResult(
                (false, $"Connection failed to {_serverAddress}:{_serverPort}"));
        }
        
        private void OnDisconnected(object sender, DisconnectedEventArgs e)
        {
            OnDisconnected();
            
            _connectCompletionSource.TrySetResult((false, $"Disconnected from {_serverAddress}: {_serverPort}"));
        }
        
        private void OnOtherClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            OnOtherClientDisconnected(e.Id);
        }
        
        public override void Disconnect()
        {
            _client.Disconnect();
            
            _client.Connected -= OnConnected;
            _client.ClientConnected -= OnOtherClientConnected;
            _client.ConnectionFailed -= OnConnectionFailed;
            _client.Disconnected -= OnDisconnected;
            _client.ClientDisconnected -= OnOtherClientDisconnected;
            _client.MessageReceived -= OnMessageReceived;
        }

        public override void Send(Message message)
        {
            _client.Send(message, shouldRelease: false);
        }
        
        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            OnMessageReceived(e.MessageId, e.Message);
        }
    }
}