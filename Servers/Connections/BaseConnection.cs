using Rip2p.Peers;
using Riptide;

namespace Rip2p.Servers.Connections
{
    public abstract class BaseConnection : IPeer
    {
        public abstract ushort Id { get; }
        public abstract void Send(Message message);
    }
}