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
        
        public void FixedUpdate()
        {
            _server.Update();
        }
        
        private void OnClientConnected(object sender, ServerConnectedEventArgs e)
        {
            
        }
        
        private void OnClientDisconnected(object sender, ServerDisconnectedEventArgs e)
        {
            
        }
        
        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            
        }
    }
}