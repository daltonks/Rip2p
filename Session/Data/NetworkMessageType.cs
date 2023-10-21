namespace Rip2p.Session.Data
{
    public enum NetworkMessageType : ushort
    {
        ConnectionTick,
        ServerTick,
        InitialToClientMessage,
        CreateSyncObjects,
        SyncData,
        DeleteSyncObjects
    }
}