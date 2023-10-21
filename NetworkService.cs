using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Rip2p.Session;
using Rip2p.Session.Clients;
using Rip2p.Session.Data;
using Rip2p.Session.Servers;
using UnityEngine;

namespace Rip2p
{
    public class NetworkService : MonoBehaviour
    {
        private const ushort SuggestedPort = 13337;
        private const ushort MaxClientCount = 8;
        
        public static NetworkService Instance { get; private set; }
        
        public event Action<NetworkSession> SessionChanged;
        
        // TODO: await SceneService.Instance.LoadClientScenesAsync();
        public event Func<Task> ClientSessionStarting;
        
        // TODO: await SceneService.Instance.LoadClientToHostConversionScenesAsync();
        public event Func<Task> ClientSessionStartingFailed;
        
        // TODO: await SceneService.Instance.LoadHostScenesAsync();
        public event Func<Task> ClientSessionStopped;
        
        private NetworkSession _session;
        public NetworkSession Session
        {
            get => _session;
            private set
            {
                if (_session == value)
                {
                    return;
                }
                
                _session = value;
                SessionChanged?.Invoke(value);
            }
        }

        private Dictionary<ushort, Type> _dataTypes;
        public IReadOnlyDictionary<ushort, Type> DataTypes => _dataTypes;

        private Dictionary<Type, ushort> _dataTypeIds;
        public IReadOnlyDictionary<Type, ushort> DataTypeIds => _dataTypeIds;
        
        public NetworkService()
        {
            Instance = this;
        }

        public void Init(params Assembly[] networkDataAssemblies)
        {
            _dataTypes = networkDataAssemblies
                .SelectMany(x => x.DefinedTypes)
                .Where(t => typeof(INetworkData).IsAssignableFrom(t))
                .Where(t => !t.IsAbstract)
                .OrderBy(t => t.AssemblyQualifiedName)
                .Select((t, i) => new { Id = (ushort) i, Type = t.AsType() })
                .ToDictionary(x => x.Id, x => x.Type);
            
            _dataTypeIds = _dataTypes.ToDictionary(x => x.Value, x => x.Key);
        }
        
        public async UniTask<(bool success, string message)> TryStartSessionAsync(
            string hostAddress = null, 
            ushort hostPort = 0)
        {
            await StopSessionAsync(invokeEvents: false);
            
            if (hostAddress != null && ClientSessionStarting != null)
            {
                await ClientSessionStarting.Invoke();
            }
            
            Session = gameObject.AddComponent<NetworkSession>();
            
            var result = hostAddress == null
                ? await Session.TryStartAsync<RiptideServer, RiptideClient>(SuggestedPort, MaxClientCount)
                : await Session.TryStartAsync<RiptideClient>(hostAddress, hostPort);

            if (!result.success)
            {
                await StopSessionAsync(invokeEvents: false);

                if (hostAddress != null && ClientSessionStartingFailed != null)
                {
                    await ClientSessionStartingFailed.Invoke();
                }
            }

            return result;
        }

        public UniTask StopSessionAsync()
        {
            return StopSessionAsync(invokeEvents: true);
        }
        
        private async UniTask StopSessionAsync(bool invokeEvents)
        {
            if (Session == null)
            {
                return;
            }

            var session = Session;
            Session = null;

            session.Stop();
            Destroy(session);
            
            if (invokeEvents && !session.IsHost && ClientSessionStopped != null)
            {
                await ClientSessionStopped.Invoke();
            }
        }

        private void OnApplicationQuit()
        {
            _ = StopSessionAsync(invokeEvents: false);
        }
    }
}