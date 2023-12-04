using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Rip2p.Session.Data;
using Rip2p.Session.Servers.Connections;
using Rip2p.Session.Syncs;
using Rip2p.Util;
using Riptide;
using UnityEngine;
using UnityEngine.SceneManagement;
using Util;

namespace Rip2p.Session
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
            
            foreach (var networkSync in createdToSendOverNetwork)
            {
                networkSync.OnOwnedDataSyncedToAll();
            }
            foreach (var networkSync in updatedToSendOverNetwork)
            {
                networkSync.OnOwnedDataSyncedToAll();
            }
            foreach (var networkSync in Syncs.OwnedAndSentOnTick)
            {
                networkSync.OnOwnedDataSyncedToAll();
            }
        }
        
        protected override void OnServerClientConnected(BaseConnection connection)
        {
            if (DetailedLogging && connection.Id != ClientId)
            {
                foreach (var networkSync in NetworkSync.All.OrderBy(x => x.Transform.GetAbsolutePath()))
                {
                    Debug.Log($"Sending create message to client \"{connection.Id}\" for:\n{networkSync}");
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
                        WriteCreateSyncObjects(message, NetworkSync.All.OrderBy(x => x.Transform.GetAbsolutePath()));
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
            if (!Syncs.ByClientId.TryGetValue(connection.Id, out var networkSyncs))
            {
                return;
            }

            if (DetailedLogging)
            {
                foreach (var networkSync in networkSyncs)
                {
                    Debug.Log($"Sending delete message to all clients for:\n{networkSync}");
                }
            }
            
            SendToAllClientsIncludingMyself(
                MessageSendMode.Reliable,
                NetworkMessageType.DeleteSyncObjects,
                message => WriteDeleteSyncObjects(message, networkSyncs.Select(x => x.Id)));
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
            IEnumerable<NetworkSync> networkSyncs)
        {
            foreach (var networkSync in networkSyncs)
            {
                WriteSyncData(message, networkSync);
            }
        }

        private void WriteSyncData(Message message, NetworkSync networkSync)
        {    
            message.AddUShort(networkSync.Id);
            networkSync.WriteTo(message);
        }

        private void ReadSyncData(Message message)
        {
            while (message.UnreadLength > 0)
            {
                var id = message.GetUShort();
                var dataTypeId = message.GetUShort();
                var dataType = NetworkService.Instance.DataTypes[dataTypeId];
                var data = NetworkDataWrapper.GetFromCache(dataType);
                data.Value.ReadFrom(message);
                
                if(Syncs.ById.TryGetValue(id, out var networkSync))
                {
                    networkSync.OnReceivedData(data);
                    
                    if (DetailedLogging && !networkSync.SendDataOnTick)
                    {
                        Debug.Log($"Received data update for:\n{networkSync}");
                    }
                }
                
                data.RemoveUsage();
            }
        }
        
        private void WriteCreateSyncObjects(
            Message message,
            IEnumerable<NetworkSync> networkSyncs)
        {
            foreach (var networkSync in networkSyncs)
            {
                message.AddUShort(networkSync.OwnerClientId);
                message.AddUShort(networkSync.Id);
                message.AddString(networkSync.Transform.GetAbsolutePath());
                networkSync.WriteTo(message);
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
                var data = NetworkDataWrapper.GetFromCache(dataType);
                data.Value.ReadFrom(message);

                var networkSync = Syncs.GetOrCreateNetworkSync(
                    id,
                    ownerClientId,
                    path,
                    dataTypeId, 
                    data,
                    rootGameObjects);
                if (networkSync != null)
                {
                    networkSync.OnReceivedData(data);
                    
                    if (DetailedLogging)
                    {
                        Debug.Log($"Client received create message for:\n{networkSync}");
                    }
                }

                data.RemoveUsage();
            }
        }

        private void WriteDeleteSyncObjects(Message message, IEnumerable<ushort> networkSyncIds)
        {
            foreach (var networkSyncId in networkSyncIds)
            {
                message.AddUShort(networkSyncId);
            }
        }
        
        private void ReadDeleteSyncObjects(Message message)
        {
            while (message.UnreadLength > 0)
            {
                var networkSyncId = message.GetUShort();
                if (Syncs.ById.TryGetValue(networkSyncId, out var networkSync))
                {
                    networkSync.Destroy();
                }
            }
        }
        
        private void OnDestroy()
        {
            Syncs.Dispose();
        }
    }
}