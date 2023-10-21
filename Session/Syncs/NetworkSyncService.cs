using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Rip2p.Session.Data;
using UnityEngine;
using Util;
using Object = UnityEngine.Object;

namespace Rip2p.Session.Syncs
{
    public class NetworkSyncService : IDisposable
    {
        public IEnumerable<NetworkSyncSession> All => _sessionToSyncMap.Keys;
        public IReadOnlyCollection<NetworkSyncSession> SentOnTick => _sentOnTick;
        public IReadOnlyCollection<NetworkSyncSession> Owned => _owned;
        public IReadOnlyCollection<NetworkSyncSession> OwnedAndSentOnTick => _ownedAndSentOnTick;
        public IReadOnlyDictionary<ushort, NetworkSyncSession> ById => _byId;
        public IReadOnlyDictionary<ushort, HashSet<NetworkSyncSession>> ByClientId => _byClientId;

        private readonly Dictionary<NetworkSync, NetworkSyncSession> _syncToSessionMap = new();
        private readonly Dictionary<NetworkSyncSession, NetworkSync> _sessionToSyncMap = new();
        private readonly HashSet<NetworkSyncSession> _sentOnTick = new();
        private readonly HashSet<NetworkSyncSession> _owned = new();
        private readonly HashSet<NetworkSyncSession> _ownedAndSentOnTick = new();
        private readonly Dictionary<ushort, NetworkSyncSession> _byId = new();
        private readonly Dictionary<ushort, HashSet<NetworkSyncSession>> _byClientId = new();

        private readonly HashSet<NetworkSyncSession> _dirtied = new();

        private ushort _maxId;
        private ushort _nextId;
        private readonly List<ushort> _freedIds = new();

        private ushort _clientId;
        
        public void Init(ushort clientId, ushort minId, ushort maxId)
        {
            _maxId = maxId;
            _nextId = minId;
            _clientId = clientId;
            
            foreach (var networkSync in NetworkSync.All.Where(x => x.IsOwned))
            {
                var syncSession = Add(GenerateId(), _clientId, networkSync);
                syncSession.OnDirtied();
            }
            NetworkSync.SyncIsNowOwned += OnSyncIsNowOwned;
        }

        private void OnSyncIsNowOwned(NetworkSync networkSync)
        {
            var syncSession = Add(GenerateId(), _clientId, networkSync);
            syncSession.OnDirtied();
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
        
        private NetworkSyncSession Add(
            ushort id, 
            ushort ownerClientId,
            NetworkSync networkSync)
        {
            var syncSession = new NetworkSyncSession(id, ownerClientId, networkSync);
            syncSession.Dirtied += OnSessionDataDirtied;

            _syncToSessionMap[networkSync] = syncSession;
            _sessionToSyncMap[syncSession] = networkSync;

            _byId[id] = syncSession;
            
            if (!_byClientId.TryGetValue(ownerClientId, out var byClientIdSet))
            {
                byClientIdSet = _byClientId[ownerClientId] = new HashSet<NetworkSyncSession>();
            }
            byClientIdSet.Add(syncSession);
            
            if (networkSync.SendDataOnTick)
            {
                _sentOnTick.Add(syncSession);
            }

            if (networkSync.IsOwned)
            {
                _owned.Add(syncSession);
            }

            if (networkSync.SendDataOnTick && networkSync.IsOwned)
            {
                _ownedAndSentOnTick.Add(syncSession);
            }

            return syncSession;
        }

        private void Remove(NetworkSyncSession syncSession)
        {
            if (!_sessionToSyncMap.TryGetValue(syncSession, out var networkSync))
            {
                return;
            }
            
            if (NetworkSession.DetailedLogging)
            {
                Debug.Log($"Removing:\n{syncSession}");
            }
            
            syncSession.Dirtied -= OnSessionDataDirtied;

            if (syncSession.IsOwned)
            {
                _freedIds.Add(syncSession.Id);
            }

            _syncToSessionMap.Remove(networkSync);
            _sessionToSyncMap.Remove(syncSession);

            _sentOnTick.Remove(syncSession);
            _owned.Remove(syncSession);
            _ownedAndSentOnTick.Remove(syncSession);
            
            _byId.Remove(syncSession.Id);
            
            var byClientIdSet = _byClientId[syncSession.OwnerClientId];
            byClientIdSet.Remove(syncSession);
            if (!byClientIdSet.Any())
            {
                _byClientId.Remove(syncSession.OwnerClientId);
            }
            
            syncSession.Dispose();
        }

        private void OnSessionDataDirtied(NetworkSyncSession syncSession)
        {
            _dirtied.Add(syncSession);
        }

        private List<NetworkSyncSession> _createdToSendOverNetwork = new();
        private readonly List<NetworkSyncSession> _updatedToSendOverNetwork = new();
        private readonly List<ushort> _deletedToSendOverNetwork = new();
        private readonly List<NetworkSyncSession> _toRemove = new();
        public void Tick(
            out IReadOnlyList<NetworkSyncSession> createdToSendOverNetwork,
            out IReadOnlyList<NetworkSyncSession> updatedToSendOverNetwork,
            out IReadOnlyList<ushort> deletedToSendOverNetwork)
        {
            _createdToSendOverNetwork.Clear();
            _updatedToSendOverNetwork.Clear();
            _deletedToSendOverNetwork.Clear();
            
            foreach (var syncSession in _dirtied)
            {
                if (syncSession.IsOwned)
                {
                    if (syncSession.CreateMessageSent)
                    {
                        if (syncSession.IsDestroyed)
                        {
                            _deletedToSendOverNetwork.Add(syncSession.Id);
                        }
                        else
                        {
                            _updatedToSendOverNetwork.Add(syncSession);
                        }
                    }
                    else if (!syncSession.IsDestroyed)
                    {
                        syncSession.CreateMessageSent = true;
                        _createdToSendOverNetwork.Add(syncSession);
                    }
                }

                if (syncSession.IsDestroyed)
                {
                    _toRemove.Add(syncSession);
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

            foreach (var syncSession in _toRemove)
            {
                Remove(syncSession);
            }
            
            createdToSendOverNetwork = _createdToSendOverNetwork;
            updatedToSendOverNetwork = _updatedToSendOverNetwork;
            deletedToSendOverNetwork = _deletedToSendOverNetwork;
        }

        [CanBeNull]
        public NetworkSyncSession GetOrCreateSyncSession(
            ushort id,
            ushort ownerClientId,
            string path,
            ushort dataTypeId,
            NetworkData data,
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
                return Add(id, ownerClientId, networkSyncs[0]);
            }

            Debug.LogError($"{networkSyncs.Count} {nameof(NetworkSync)}s found for " +
                           $"{NetworkService.Instance.DataTypes[dataTypeId]} on instantiated GameObject at path \"{path}\". " +
                           $"Destroying the GameObject.");
                        
            Object.Destroy(networkGameObject);
            return null;
        }
        
        public void Dispose()
        {
            NetworkSync.SyncIsNowOwned -= OnSyncIsNowOwned;
            
            // Destroy non-owned syncs
            var nonOwned = _syncToSessionMap
                .Where(x => !x.Key.IsOwned)
                .ToList();
            foreach (var (sync, syncSession) in nonOwned)
            {
                Remove(syncSession);
                sync.Destroy();
            }
        }
    }
}