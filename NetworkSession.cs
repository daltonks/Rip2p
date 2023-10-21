using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Rip2p.Servers.Connections;
using Riptide;
using UnityEngine;
using UnityEngine.SceneManagement;
using Util;

namespace Rip2p
{
    public class NetworkSession : P2PSession<NetworkMessageType>
    {
        public const bool DetailedLogging = true;
        
        public const int FixedUpdatesBetweenTicks = 3;
        
        public bool IsConnected { get; private set; }

        public NetworkSyncService Syncs { get; } = new();
        private readonly NetworkSyncIdRangeService _idRangeService = new();

        private int _tickIndex;
        protected override void Tick()
        {
            unchecked
            {
                ++_tickIndex;
            }

            if (_tickIndex % FixedUpdatesBetweenTicks != 0)
            {
                return;
            }

            if (!IsConnected)
            {
                return;
            }
            
            Syncs.Tick(
                out var createdToSendOverNetwork, 
                out var updatedToSendOverNetwork, 
                out var deletedToSendOverNetwork);

            if (DetailedLogging && (createdToSendOverNetwork.Any() || updatedToSendOverNetwork.Any() || deletedToSendOverNetwork.Any()))
            {
                Debug.Log("Sending dirty batch to other clients:\n\n" +
                          $"Created:\n\n{string.Join("\n", createdToSendOverNetwork)}\n" +
                          $"Updated:\n\n{string.Join("\n", updatedToSendOverNetwork)}\n" +
                          $"Deleted: {string.Join(", ", deletedToSendOverNetwork)}\n\n");
            }
            
            // Send created NetworkSyncs
            if (createdToSendOverNetwork.Any())
            {
                SendToOtherClients(
                    MessageSendMode.Reliable,
                    NetworkMessageType.CreateSyncObjects,
                    message => WriteCreateSyncObjects(message, createdToSendOverNetwork));
            }
                
            // Send updated NetworkSyncs
            if (updatedToSendOverNetwork.Any())
            {
                SendToOtherClients(
                    MessageSendMode.Reliable,
                    NetworkMessageType.SyncData,
                    message => WriteSyncData(message, updatedToSendOverNetwork));
            }

            // Send deleted NetworkSyncs
            if (deletedToSendOverNetwork.Any())
            {
                SendToOtherClients(
                    MessageSendMode.Reliable,
                    NetworkMessageType.DeleteSyncObjects,
                    message => WriteDeleteSyncObjects(message, deletedToSendOverNetwork));
            }
            
            // Send tick
            if (IsHost)
            {
                SendToOtherClients(
                    MessageSendMode.Unreliable,
                    NetworkMessageType.ServerTick,
                    message => WriteSyncData(message, Syncs.SentOnTick));
            }
            else
            {
                SendToServer(
                    MessageSendMode.Unreliable,
                    NetworkMessageType.ConnectionTick,
                    message => WriteSyncData(message, Syncs.OwnedAndSentOnTick));
            }
            
            foreach (var syncSession in createdToSendOverNetwork)
            {
                syncSession.OnOwnedDataSyncedToAll();
            }
            foreach (var syncSession in updatedToSendOverNetwork)
            {
                syncSession.OnOwnedDataSyncedToAll();
            }
            foreach (var syncSession in Syncs.OwnedAndSentOnTick)
            {
                syncSession.OnOwnedDataSyncedToAll();
            }
        }
        
        protected override void OnServerClientConnected(BaseConnection connection)
        {
            if (DetailedLogging && connection.Id != ClientId)
            {
                foreach (var syncSession in Syncs.All.OrderBy(x => x.Transform.GetAbsolutePath()))
                {
                    Debug.Log($"Sending create message to client \"{connection.Id}\" for:\n{syncSession}");
                }
            }
            
            SendToClient(
                MessageSendMode.Reliable,
                NetworkMessageType.InitialToClientMessage,
                message =>
                {
                    var syncIdRange = _idRangeService.GetFreeRange();
                    message.AddUShort(syncIdRange.minId);
                    message.AddUShort(syncIdRange.maxId);
                    connection.SyncIdMin = syncIdRange.minId;
                    
                    if (connection.Id != ClientId)
                    {
                        WriteCreateSyncObjects(message, Syncs.All.OrderBy(x => x.Transform.GetAbsolutePath()));
                    }
                },
                connection.Id);
        }

        protected override void OnMyClientConnected(ushort clientId)
        {
            IsConnected = true;
        }

        protected override void OnOtherClientConnected(ushort clientId)
        {
            
        }

        protected override void OnServerClientDisconnected(BaseConnection connection)
        {
            _idRangeService.FreeRange(connection.SyncIdMin);
            
            // Delete syncs owned by this connection
            if (!Syncs.ByClientId.TryGetValue(connection.Id, out var syncSessions))
            {
                return;
            }

            if (DetailedLogging)
            {
                foreach (var syncSession in syncSessions)
                {
                    Debug.Log($"Sending delete message to all clients for:\n{syncSession}");
                }
            }
            
            SendToAllClientsIncludingMyself(
                MessageSendMode.Reliable,
                NetworkMessageType.DeleteSyncObjects,
                message => WriteDeleteSyncObjects(message, syncSessions.Select(x => x.Id)));
        }

        protected override void OnMyClientDisconnected(ushort clientId)
        {
            _ = NetworkService.Instance.StopSessionAsync();
        }

        protected override void OnOtherClientDisconnected(ushort clientId)
        {
            
        }

        protected override void OnServerMessageReceived(BaseConnection connection, NetworkMessageType messageType, Message message)
        {
            switch (messageType)
            {
                case NetworkMessageType.ConnectionTick:
                    ReadSyncData(message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(messageType), messageType, null);
            }
        }
        
        protected override void OnClientMessageReceived(NetworkMessageType messageType, Message message)
        {
            switch (messageType)
            {
                case NetworkMessageType.ServerTick:
                    ReadSyncData(message);
                    break;

                case NetworkMessageType.InitialToClientMessage:
                    var minId = message.GetUShort();
                    var maxId = message.GetUShort();
                    
                    if (!IsHost)
                    {
                        ReadCreateSyncObjects(message);
                    }
                    
                    Syncs.Init(ClientId, minId, maxId);
                    
                    MessagingService.Instance.Raise(ReceivedInitialDataFromServerMessage.Instance);
                    break;
                
                case NetworkMessageType.CreateSyncObjects:
                    ReadCreateSyncObjects(message);
                    break;
                
                case NetworkMessageType.SyncData:
                    ReadSyncData(message);
                    break;
                
                case NetworkMessageType.DeleteSyncObjects:
                    ReadDeleteSyncObjects(message);
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(messageType), messageType, null);
            }
        }
        
        private void WriteSyncData(
            Message message,
            IEnumerable<NetworkSyncSession> syncSessions)
        {
            foreach (var syncSession in syncSessions)
            {
                WriteSyncData(message, syncSession);
            }
        }

        private void WriteSyncData(Message message, NetworkSyncSession syncSession)
        {    
            message.AddUShort(syncSession.Id);
            syncSession.WriteTo(message);
        }

        private void ReadSyncData(Message message)
        {
            while (message.UnreadLength > 0)
            {
                var id = message.GetUShort();
                var dataTypeId = message.GetUShort();
                var dataType = NetworkService.Instance.DataTypes[dataTypeId];
                var data = NetworkData.GetFromCache(dataType);
                data.Value.ReadFrom(message);
                
                if(Syncs.ById.TryGetValue(id, out var syncSession))
                {
                    syncSession.OnReceivedData(data);
                    
                    if (DetailedLogging && !syncSession.SendDataOnTick)
                    {
                        Debug.Log($"Received data update for:\n{syncSession}");
                    }
                }
                
                data.RemoveUsage();
            }
        }
        
        private void WriteCreateSyncObjects(
            Message message,
            IEnumerable<NetworkSyncSession> syncSessions)
        {
            foreach (var syncSession in syncSessions)
            {
                message.AddUShort(syncSession.OwnerClientId);
                message.AddUShort(syncSession.Id);
                message.AddString(syncSession.Transform.GetAbsolutePath());
                syncSession.WriteTo(message);
            }
        }

        [SuppressMessage("ReSharper", "Unity.NoNullPropagation")]
        private void ReadCreateSyncObjects(Message message)
        {
            var rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects()
                .ToDictionary(x => x.name, x => x);
            
            while (message.UnreadLength > 0)
            {
                var ownerClientId = message.GetUShort();
                var id = message.GetUShort();
                var path = message.GetString();
                var dataTypeId = message.GetUShort();
                var dataType = NetworkService.Instance.DataTypes[dataTypeId];
                var data = NetworkData.GetFromCache(dataType);
                data.Value.ReadFrom(message);

                var syncSession = Syncs.GetOrCreateSyncSession(
                    id,
                    ownerClientId,
                    path,
                    dataTypeId, 
                    data,
                    rootGameObjects);
                if (syncSession != null)
                {
                    syncSession.OnReceivedData(data);
                    
                    if (DetailedLogging)
                    {
                        Debug.Log($"Client received create message for:\n{syncSession}");
                    }
                }

                data.RemoveUsage();
            }
        }

        private void WriteDeleteSyncObjects(Message message, IEnumerable<ushort> syncSessionIds)
        {
            foreach (var syncSessionId in syncSessionIds)
            {
                message.AddUShort(syncSessionId);
            }
        }
        
        private void ReadDeleteSyncObjects(Message message)
        {
            while (message.UnreadLength > 0)
            {
                var networkSyncId = message.GetUShort();
                if (Syncs.ById.TryGetValue(networkSyncId, out var syncSession))
                {
                    syncSession.Destroy();
                }
            }
        }
        
        private void OnDestroy()
        {
            Syncs.Dispose();
        }
    }
}