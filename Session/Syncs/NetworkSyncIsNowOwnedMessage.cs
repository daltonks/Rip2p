namespace Rip2p.Session.Syncs
{
    public class NetworkSyncIsNowOwnedMessage
    {
        public NetworkSync NetworkSync { get; }

        public NetworkSyncIsNowOwnedMessage(NetworkSync networkSync)
        {
            NetworkSync = networkSync;
        }
    }
}