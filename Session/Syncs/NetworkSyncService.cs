using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Rip2p.Session.Data;
using Rip2p.Util;
using UnityEngine;
using Util;
using Object = UnityEngine.Object;

namespace Rip2p.Session.Syncs
{
    public class NetworkSyncService : IDisposable
    {
        public IReadOnlyCollection<NetworkSync> SentOnTick => _sentOnTick;
        public IReadOnlyCollection<NetworkSync> Owned => _owned;
        public IReadOnlyCollection<NetworkSync> OwnedAndSentOnTick => _ownedAndSentOnTick;
        public IReadOnlyDictionary<ushort, NetworkSync> ById => _byId;
        public IReadOnlyDictionary<ushort, HashSet<NetworkSync>> ByClientId => _byClientId;

        private readonly HashSet<NetworkSync> _sentOnTick = new();
        private readonly HashSet<NetworkSync> _owned = new();
        private readonly HashSet<NetworkSync> _ownedAndSentOnTick = new();
        private readonly Dictionary<ushort, NetworkSync> _byId = new();
        private readonly Dictionary<ushort, HashSet<NetworkSync>> _byClientId = new();

        private readonly HashSet<NetworkSync> _dirtied = new();

        private ushort _maxId;
        private ushort _nextId;
        private readonly List<ushort> _freedIds = new();

        private ushort _clientId;

        public NetworkSyncService()
        {
            MessagingService.Instance.AddListener<NetworkSyncIsNowOwnedMessage>(OnSyncIsNowOwned);
        }
        
        public void Init(ushort clientId, ushort minId, ushort maxId)
        {
            _maxId = maxId;
            _nextId = minId;
            _clientId = clientId;
            
            foreach (var networkSync in NetworkSync.All.Where(x => x.IsOwned))
            {
                Add(GenerateId(), _clientId, networkSync);
                networkSync.OnDirtied();
            }
        }

        private void OnSyncIsNowOwned(NetworkSyncIsNowOwnedMessage message)
        {
            if (_maxId == 0)
            {
                return;
            }
            Add(GenerateId(), _clientId, message.NetworkSync);
        }

        private ushort GenerateId()
        {
            if (_freedIds.Any())
            {
                var id = _freedIds.Last();
                _freedIds.RemoveAt(_freedIds.Count - 1);
                return id;
            }

            if (_nextId > _maxId)
            {
                throw new Exception("Ran out of sync IDs!");
            }

            return _nextId++;
        }
        
        private void Add(
            ushort id, 
            ushort ownerClientId,
            NetworkSync networkSync)
        {
            networkSync.OnSessionBegin(id, ownerClientId);
            networkSync.Dirtied += OnNetworkSyncDirtied;
            networkSync.DestroyBegin += OnNetworkSyncDestroyBegin;

            _byId[id] = networkSync;
            
            if (!_byClientId.TryGetValue(ownerClientId, out var byClientIdSet))
            {
                byClientIdSet = _byClientId[ownerClientId] = new HashSet<NetworkSync>();
            }
            byClientIdSet.Add(networkSync);
            
            if (networkSync.SendDataOnTick)
            {
                _sentOnTick.Add(networkSync);
            }

            if (networkSync.IsOwned)
            {
                _owned.Add(networkSync);
            }

            if (networkSync.SendDataOnTick && networkSync.IsOwned)
            {
                _ownedAndSentOnTick.Add(networkSync);
            }
        }

        private void Remove(NetworkSync networkSync)
        {
            networkSync.OnSessionEnd();
            
            if (NetworkSession.DetailedLogging)
            {
                Debug.Log($"Removing:\n{networkSync}");
            }
            
            networkSync.Dirtied -= OnNetworkSyncDirtied;
            networkSync.DestroyBegin -= OnNetworkSyncDestroyBegin;

            if (networkSync.IsOwned)
            {
                _freedIds.Add(networkSync.Id);
            }

            _sentOnTick.Remove(networkSync);
            _owned.Remove(networkSync);
            _ownedAndSentOnTick.Remove(networkSync);
            
            _byId.Remove(networkSync.Id);
            
            var byClientIdSet = _byClientId[networkSync.OwnerClientId];
            byClientIdSet.Remove(networkSync);
            if (!byClientIdSet.Any())
            {
                _byClientId.Remove(networkSync.OwnerClientId);
            }
        }
        
        private void OnNetworkSyncDestroyBegin(NetworkSync networkSync)
        {
            Remove(networkSync);
        }
        
        private void OnNetworkSyncDirtied(NetworkSync networkSync)
        {
            _dirtied.Add(networkSync);
        }

        private List<NetworkSync> _createdToSendOverNetwork = new();
        private readonly List<NetworkSync> _updatedToSendOverNetwork = new();
        private readonly List<ushort> _deletedToSendOverNetwork = new();
        public void Tick(
            out IReadOnlyList<NetworkSync> createdToSendOverNetwork,
            out IReadOnlyList<NetworkSync> updatedToSendOverNetwork,
            out IReadOnlyList<ushort> deletedToSendOverNetwork)
        {
            _createdToSendOverNetwork.Clear();
            _updatedToSendOverNetwork.Clear();
            _deletedToSendOverNetwork.Clear();
            
            foreach (var networkSync in _dirtied)
            {
                if (networkSync.IsOwned)
                {
                    if (networkSync.CreateMessageSent)
                    {
                        if (networkSync.IsDestroyed)
                        {
                            _deletedToSendOverNetwork.Add(networkSync.Id);
                        }
                        else
                        {
                            _updatedToSendOverNetwork.Add(networkSync);
                        }
                    }
                    else if (!networkSync.IsDestroyed)
                    {
                        networkSync.CreateMessageSent = true;
                        _createdToSendOverNetwork.Add(networkSync);
                    }
                }
            }
            
            _dirtied.Clear();

            // Order _createNetworkingNeeded by path to create them in the proper parent/child order
            if (_createdToSendOverNetwork.Any())
            {
                _createdToSendOverNetwork = _createdToSendOverNetwork
                    .OrderBy(x => x.Transform.GetAbsolutePath())
                    .ToList();
            }
            
            createdToSendOverNetwork = _createdToSendOverNetwork;
            updatedToSendOverNetwork = _updatedToSendOverNetwork;
            deletedToSendOverNetwork = _deletedToSendOverNetwork;
        }

        [CanBeNull]
        public NetworkSync GetOrCreateNetworkSync(
            ushort id,
            ushort ownerClientId,
            string path,
            ushort dataTypeId,
            NetworkDataWrapper data,
            Dictionary<string, GameObject> rootGameObjects)
        {
            GameObject networkGameObject;
            
            var prefab = data.Value.GetPrefab();
            
            var lastSlashIndex = path.LastIndexOf("/", StringComparison.Ordinal);
            var networkGameObjectName = path[(lastSlashIndex + 1)..];
            var findName = networkGameObjectName[..networkGameObjectName.LastIndexOf(" ", StringComparison.Ordinal)];
            if (lastSlashIndex == -1)
            {
                if (!rootGameObjects.TryGetValue(findName, out networkGameObject))
                {
                    networkGameObject = Object.Instantiate(prefab);
                    rootGameObjects[networkGameObjectName] = networkGameObject;
                }
            }
            else
            {
                var firstSlashIndex = path.IndexOf("/", StringComparison.Ordinal);
                var rootName = path[..firstSlashIndex];
                
                if (!rootGameObjects.TryGetValue(rootName, out var rootGameObject))
                {
                    Debug.LogError($"Root game object \"{rootName}\" not found when attempting to create NetworkSync. \n" +
                                   $"Data Type: {NetworkService.Instance.DataTypes[dataTypeId]}.");
                    return null;
                }

                var substring = path.Substring(
                    firstSlashIndex + 1, 
                    path.LastIndexOf(" ", StringComparison.Ordinal) - (firstSlashIndex + 1));
                networkGameObject = rootGameObject.transform.Find(substring)?.gameObject;
                if (networkGameObject == null)
                {
                    if (firstSlashIndex == lastSlashIndex)
                    {
                        networkGameObject = Object.Instantiate(prefab, rootGameObject.transform);
                    }
                    else
                    {
                        var rootToParentPath = path.Substring(firstSlashIndex + 1, lastSlashIndex - (firstSlashIndex + 1));
                        var parent = rootGameObjects.First().Value.transform.root.Find(rootToParentPath);

                        if (parent == null)
                        {
                            Debug.LogError($"Parent at path \"{rootName}/{rootToParentPath}\" not found when attempting to create NetworkSync. \n" +
                                           $"Data Type: {NetworkService.Instance.DataTypes[dataTypeId]}.");
                    
                            return null;
                        }

                        networkGameObject = Object.Instantiate(prefab, parent);
                    }
                }
            }
            
            networkGameObject.name = networkGameObjectName;

            var networkSyncs = networkGameObject
                .GetComponents<NetworkSync>()
                .Where(x => x.DataTypeId == dataTypeId)
                .ToList();
                    
            if (networkSyncs.Count == 1)
            {
                var networkSync = networkSyncs[0];
                Add(id, ownerClientId, networkSync);
                return networkSync;
            }

            Debug.LogError($"{networkSyncs.Count} {nameof(NetworkSync)}s found for " +
                           $"{NetworkService.Instance.DataTypes[dataTypeId]} on instantiated GameObject at path \"{path}\". " +
                           $"Destroying the GameObject.");
                        
            Object.Destroy(networkGameObject);
            return null;
        }
        
        public void Dispose()
        {
            MessagingService.Instance.RemoveListener<NetworkSyncIsNowOwnedMessage>(OnSyncIsNowOwned);
            
            // Destroy non-owned syncs
            var nonOwned = NetworkSync.All
                .Where(x => !x.IsOwned)
                .ToList();
            foreach (var networkSync in nonOwned)
            {
                networkSync.Destroy();
            }
        }
    }
}