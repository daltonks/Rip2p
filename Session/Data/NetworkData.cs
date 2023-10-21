using System;
using System.Collections.Generic;

namespace Rip2p.Session.Data
{
    public class NetworkData
    {
        public static NetworkData<TData> GetFromCache<TData>() 
            where TData : class, INetworkData, new()
        {
            var data = GetCachedObject<TData>();
            var networkData = GetCachedObject<NetworkData<TData>>();
            ((NetworkData)networkData).Value = data;
            networkData._usages = 1;
            return networkData;
        }
        
        public static NetworkData GetFromCache(Type type)
        {
            var data = GetCachedObject(type);
            var networkData = GetCachedObject<NetworkData>();
            networkData.Value = (INetworkData)data;
            networkData._usages = 1;
            return networkData;
        }

        private static readonly Dictionary<Type, List<object>> CachedData = new();

        private static TData GetCachedObject<TData>() where TData : class, new()
        {
            return (TData) GetCachedObject(typeof(TData));
        }
        
        private static object GetCachedObject(Type type)
        {
            if (CachedData.TryGetValue(type, out var list) && list.Count > 0)
            {
                var data = list[^1];
                list.RemoveAt(list.Count - 1);
                return data;
            }

            return Activator.CreateInstance(type);
        }

        private static void ReturnCachedObject(object obj)
        {
            var type = obj.GetType();
            if (!CachedData.TryGetValue(type, out var list))
            {
                list = CachedData[type] = new List<object>();
            }
            
            list.Add(obj);
        }

        public INetworkData Value { get; private set; }

        private int _usages;

        public void AddUsage()
        {
            _usages++;
        }

        public void RemoveUsage()
        {
            if (_usages == 0)
            {
                throw new Exception(
                    $"{nameof(NetworkData)}: 0 usages remaining when attempting to remove a usage.");
            }
            
            _usages--;
            if (_usages == 0)
            {
                ReturnCachedObject(Value);
                ReturnCachedObject(this);
                
                Value = null;
            }
        }
    }

    public class NetworkData<TData> : NetworkData
    {
        public new TData Value => (TData)base.Value;
    }
}