using Riptide;

namespace Rip2p.Peers
{
    public interface IPeer
    {
        ushort Id { get; }
        void Send(Message message);
    }
}