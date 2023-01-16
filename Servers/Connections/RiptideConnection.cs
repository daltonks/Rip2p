using Riptide;

namespace Rip2p.Servers.Connections
{
    public class RiptideConnection : BaseConnection
    {
        public override ushort Id => Connection.Id;
        public Connection Connection { get; }
        
        private readonly RiptideServer _server;

        public RiptideConnection(Connection connection, RiptideServer server)
        {
            Connection = connection;
            _server = server;
        }
        
        public override void Send(Message message)
        {
            _server.Send(message, this);
        }
    }
}