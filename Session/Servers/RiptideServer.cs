using System.Collections.Generic;
using Rip2p.Session.Servers.Connections;
using Riptide;

namespace Rip2p.Session.Servers
{
    public class RiptideServer : BaseServer
    {
        private readonly Dictionary<ushort, RiptideConnection> _connections = new();
        
        private RiptideServerImpl _server;

        private void Awake()
        {
            _server = new RiptideServerImpl();
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

        public override void Tick()
        {
            _server.Update();
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

        public override void Send(Message message, ushort clientId)
        {
            _server.Send(message, clientId, shouldRelease: false);
        }

        public override void SendToAllExcept(Message message, ushort client)
        {
            _server.SendToAll(message, exceptToClientId: client, shouldRelease: false);
        }

        private void OnMessageReceived(Connection fromConnection, ushort messageId, Message message)
        {
            if (_connections.TryGetValue(fromConnection.Id, out var connection))
            {
                OnMessageReceived(connection, messageId, message);
            }
        }
    }

    class RiptideServerImpl : Server
    {
        public delegate void MessageReceivedDelegate(Connection fromConnection, ushort messageId, Message message);

        public new event MessageReceivedDelegate MessageReceived;
        
        protected override void OnMessageReceived(Message message, Connection fromConnection)
        {
            var messageId = message.GetUShort();
            MessageReceived?.Invoke(fromConnection, messageId, message);
        }
    }
}