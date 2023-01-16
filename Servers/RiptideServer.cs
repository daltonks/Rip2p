using Riptide;

namespace Rip2p.Servers
{
    public class RiptideServer : BaseServer
    {
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
            OnClientConnected(e.Client.Id);
        }
        
        private void OnClientDisconnected(object sender, ServerDisconnectedEventArgs e)
        {
            OnClientDisconnected(e.Client.Id);
        }
        
        public override void SendToAll(Message message)
        {
            _server.SendToAll(message, shouldRelease: false);
        }

        public override void SendToAllExcept(Message message, ushort client)
        {
            _server.SendToAll(message, exceptToClientId: client, shouldRelease: false);
        }

        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            OnMessageReceived(e.FromConnection.Id, e.Message);
        }
        
        public void FixedUpdate()
        {
            _server.Update();
        }
    }
}