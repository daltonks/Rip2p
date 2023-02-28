namespace Rip2p.Servers.Connections
{
    public abstract class BaseConnection
    {
        public abstract ushort Id { get; }
        public ushort SyncIdMin { get; set; }
    }
}