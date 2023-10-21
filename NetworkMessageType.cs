namespace Rip2p
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