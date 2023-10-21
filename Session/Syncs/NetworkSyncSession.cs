using System;
using Rip2p.Session.Data;
using Riptide;
using UnityEngine;
using Util;

namespace Rip2p.Session.Syncs
{
    public class NetworkSyncSession : IDisposable
    {
        public event Action<NetworkSyncSession> Dirtied;
        
        private readonly NetworkSync _networkSync;
        
        public NetworkSyncSession(ushort id, ushort ownerClientId, NetworkSync networkSync)
        {
            Id = id;
            OwnerClientId = ownerClientId;
            _networkSync = networkSync;
            
            networkSync.NetworkSyncDirtied += OnNetworkSyncDirtied;
        }
        
        public ushort Id { get; }
        public ushort OwnerClientId { get; set; }
        public bool CreateMessageSent { get; set; }

        public bool IsOwned => _networkSync.IsOwned;
        public bool SendDataOnTick => _networkSync.SendDataOnTick;
        public Transform Transform => _networkSync.transform;
        public bool IsDestroyed => _networkSync.IsDestroyed;
        
        public void WriteTo(Message message)
        {
            _networkSync.WriteTo(message);
        }

        public void OnReceivedData(NetworkData data)
        {
            _networkSync.OnReceivedData(data);
        }

        public void OnDirtied()
        {
            Dirtied?.Invoke(this);
        }
        
        private void OnNetworkSyncDirtied(NetworkSync networkSync)
        {
            Dirtied?.Invoke(this);
        }

        public void OnOwnedDataSyncedToAll()
        {
            _networkSync.OnOwnedDataSyncedToAll();
        }
        
        public override string ToString()
        {
            if (_networkSync == null)
            {
                return $"ID: {Id}\n" +
                       $"IsOwned: {IsOwned}\n";
            }

            var networkData = _networkSync.CreateData();
            var serializedData = DebugSerializer.Serialize(networkData.Value);
            networkData.RemoveUsage();
            
            return $"ID: {Id}\n" +
                   $"Type: {_networkSync.GetType().Name}\n" +
                   $"IsOwned: {IsOwned}\n" +
                   $"IsDestroyed: {IsDestroyed}\n" +
                   $"Data:\n{serializedData}\n";
        }

        public void Destroy()
        {
            _networkSync.Destroy();
        }
        
        public void Dispose()
        {
            _networkSync.NetworkSyncDirtied -= OnNetworkSyncDirtied;
        }
    }
}