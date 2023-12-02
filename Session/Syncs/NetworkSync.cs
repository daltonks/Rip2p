using System;
using System.Collections.Generic;
using System.Linq;
using Rip2p.Session.Data;
using Rip2p.Util;
using Riptide;
using UnityEngine;
using Util;

namespace Rip2p.Session.Syncs
{
    public abstract class NetworkSync<TData> : NetworkSync
        where TData : class, INetworkData, new()
    {
        private ushort _dataTypeId;
        public override ushort DataTypeId => _dataTypeId;

        protected override void Awake()
        {
            _dataTypeId = NetworkService.Instance.DataTypeIds[typeof(TData)];
            
            base.Awake();
        }

        public override NetworkDataWrapper CreateData()
        {
            var networkData = NetworkDataWrapper.GetFromCache<TData>();
            UpdateData(networkData.Value);
            return networkData;
        }

        public override void WriteTo(Message message)
        {
            message.AddUShort(_dataTypeId);
        
            var data = CreateData();
            data.Value.WriteTo(message);
            data.RemoveUsage();
        
            if (IsOwned)
            {
                OnOwnedDataSyncedToAll();
            }
        }

        public override void OnReceivedData(NetworkDataWrapper networkData)
        {
            OnReceivedData(networkData, (TData)networkData.Value);
        }

        protected abstract void UpdateData(TData data);
        protected abstract void OnReceivedData(NetworkDataWrapper networkData, TData data);
    }
    
    public abstract class NetworkSync : MonoBehaviour
    {
        public static IReadOnlyCollection<NetworkSync> All => AllSyncs;

        private static readonly HashSet<NetworkSync> AllSyncs = new();
        private static readonly Dictionary<Type, List<NetworkSync>> ByTypeSyncs = new();
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStaticMembers()
        {
            AllSyncs.Clear();
            ByTypeSyncs.Clear();
        }
        
        public static IEnumerable<T> GetByType<T>() where T : NetworkSync
        {
            return ByTypeSyncs.TryGetValue(typeof(T), out var byTypeList) 
                ? byTypeList.Cast<T>() 
                : Enumerable.Empty<T>();
        }

        public event Action<NetworkSync> NetworkSyncDirtied;
        
        [SerializeField] private bool _isOwned;

        private bool? _nonSerializedIsOwned;
        public bool IsOwned
        {
            get => _nonSerializedIsOwned ?? _isOwned;
            set
            {
                if (_nonSerializedIsOwned == value)
                {
                    return;
                }

                if (_nonSerializedIsOwned == true)
                {
                    throw new Exception(
                        $"Once a {nameof(NetworkSync)}.{nameof(IsOwned)} is set to 'true', " +
                        $"it can't be changed.");
                }
                
                _nonSerializedIsOwned = _isOwned = value;
                
                if (value)
                {
                    name = $"{name} {SmallStringGuid.NewGuid()}";
                    NetworkSyncDirtied?.Invoke(this);
                    MessagingService.Instance.Raise(new NetworkSyncIsNowOwnedMessage(this));
                }
            }
        }

        public bool IsDestroyed { get; private set; }

        public virtual bool SendDataOnTick => false;
        
        public abstract ushort DataTypeId { get; }
        
        protected virtual void Awake()
        {
            AllSyncs.Add(this);

            if (!ByTypeSyncs.TryGetValue(GetType(), out var byTypeList))
            {
                byTypeList = ByTypeSyncs[GetType()] = new List<NetworkSync>();
            }
            byTypeList.Add(this);
        }

        protected virtual void Start()
        {
            IsOwned = _isOwned;
        }
        
        public void OnNetworkSyncDirtied()
        {
            if (!IsOwned)
            {
                return;
            }
            
            NetworkSyncDirtied?.Invoke(this);
        }
        
        public abstract void WriteTo(Message message);

        public abstract NetworkDataWrapper CreateData();

        public abstract void OnOwnedDataSyncedToAll();

        public abstract void OnReceivedData(NetworkDataWrapper networkData);
        
        public void Destroy()
        {
            if (!IsDestroyed)
            {
                DestroyInternal();
                Destroy(gameObject);
            }
        }
        
        protected virtual void OnDestroy()
        {
            DestroyInternal();
        }

        private void DestroyInternal()
        {
            if (IsDestroyed)
            {
                return;
            }

            IsDestroyed = true;
            AllSyncs.Remove(this);
            
            var byTypeList = ByTypeSyncs[GetType()];
            byTypeList.Remove(this);
            if (!byTypeList.Any())
            {
                ByTypeSyncs.Remove(GetType());
            }

            NetworkSyncDirtied?.Invoke(this);
        }
    }
}