using System.Collections.Generic;
using Rip2p.Servers.Connections;
using Riptide;

namespace Rip2p.Servers
{
    public class RiptideServer : BaseServer
    {
        private readonly Dictionary<ushort, RiptideConnection> _connections = new();
        
        private Server _server;

        private void Awake()
        {
            _server = new Server();
            _server.ClientConnected += OnClientConnected;
            _server.ClientDisconnected += OnClientDisconnected;
            _server.MessageReceived += OnMessageReceived;
        }

        protected override void StartServerInternal(ushort port, ushort maxClientCount)
        {
            _server.Start(port, maxClientCount: maxClientCount);
        }

        public override void StopServer()
        {
            _server.Stop();
            
            _server.ClientConnected -= OnClientConnected;
            _server.ClientDisconnected -= OnClientDisconnected;
            _server.MessageReceived -= OnMessageReceived;
        }

        private void OnClientConnected(object sender, ServerConnectedEventArgs e)
        {
            var connection = _connections[e.Client.Id] = new RiptideConnection(e.Client);
            OnClientConnected(connection);
        }
        
        private void OnClientDisconnected(object sender, ServerDisconnectedEventArgs e)
        {
            if (_connections.TryGetValue(e.Client.Id, out var connection))
            {
                OnClientDisconnected(connection);
            }
        }

        public void Send(Message message, RiptideConnection connection)
        {
            _server.Send(message, connection.Connection);
        }

        public override void Send(Message message, ushort clientId)
        {
            _server.Send(message, clientId, shouldRelease: false);
        }

        public override void SendToAllExcept(Message message, ushort client)
        {
            _server.SendToAll(message, exceptToClientId: client, shouldRelease: false);
        }

        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            if (_connections.TryGetValue(e.FromConnection.Id, out var connection))
            {
                OnMessageReceived(connection, e.MessageId, e.Message);
            }
        }
        
        public void FixedUpdate()
        {
            _server.Update();
        }
    }
}