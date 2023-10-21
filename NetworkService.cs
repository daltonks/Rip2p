using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Rip2p.Clients;
using Rip2p.Servers;
using UnityEngine;

namespace Rip2p
{
    public class NetworkService : MonoBehaviour
    {
        private const ushort SuggestedPort = 13337;
        private const ushort MaxClientCount = 8;
        
        public static NetworkService Instance { get; private set; }
        
        public event Action<NetworkSession> SessionChanged;

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
        
        private void Awake()
        {
            _dataTypes = typeof(NetworkService).Assembly.DefinedTypes
                .Where(t => typeof(IData).IsAssignableFrom(t))
                .Where(t => !t.IsAbstract)
                .OrderBy(t => t.Name)
                .Select((t, i) => new { Id = (ushort) i, Type = t.AsType() })
                .ToDictionary(x => x.Id, x => x.Type);

            _dataTypeIds = _dataTypes.ToDictionary(x => x.Value, x => x.Key);
        }
        
        public async UniTask<(bool success, string message)> TryStartSessionAsync(
            string hostAddress = null, 
            ushort hostPort = 0)
        {
            await StopSessionAsync(allowSceneChange: false);
            
            if (hostAddress != null)
            {
                await SceneService.Instance.LoadClientScenesAsync();
            }
            
            Session = gameObject.AddComponent<NetworkSession>();
            
            var result = hostAddress == null
                ? await Session.TryStartAsync<RiptideServer, RiptideClient>(SuggestedPort, MaxClientCount)
                : await Session.TryStartAsync<RiptideClient>(hostAddress, hostPort);

            if (!result.success)
            {
                await StopSessionAsync(allowSceneChange: false);

                if (hostAddress != null)
                {
                    await SceneService.Instance.LoadClientToHostConversionScenesAsync();
                }
            }

            return result;
        }

        public async UniTask StopSessionAsync(bool allowSceneChange)
        {
            if (Session == null)
            {
                return;
            }

            var session = Session;
            Session = null;

            session.Stop();
            Destroy(session);
            
            if (allowSceneChange && !session.IsHost)
            {
                await SceneService.Instance.LoadHostScenesAsync();
            }
        }

        private void OnApplicationQuit()
        {
            _ = StopSessionAsync(allowSceneChange: false);
        }
    }
}