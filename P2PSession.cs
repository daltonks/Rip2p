using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rip2p.Clients;
using Rip2p.Servers;
using UnityEngine;

namespace Rip2p
{
    public class P2PSession : MonoBehaviour
    {
        public BaseServer Server { get; private set; }
        public BaseClient HostClient { get; private set; }
        public IReadOnlyList<BaseClient> AllClients => _allClients;
        
        private readonly List<BaseClient> _allClients = new();
        
        private Type _clientImplementationType;
        
        public async Task<bool> TryStartAsync<TServer, TClient>(
            ushort suggestedPort, 
            ushort maxClientCount,
            string hostAddress,
            ushort hostPort)
            where TServer : BaseServer 
            where TClient : BaseClient
        {
            
            if (!TryStart<TServer, TClient>(suggestedPort, maxClientCount))
            {
                return false;
            }

            var (connectSuccess, client) = await TryConnectAsync(hostAddress, hostPort);
            client._isHostClient = true;
            HostClient = client;
            return connectSuccess;
        }
        
        private bool _started;
        public bool TryStart<TServer, TClient>(
            ushort suggestedPort, 
            ushort maxClientCount)
            where TServer : BaseServer 
            where TClient : BaseClient
        {
            if (_started)
            {
                return false;
            }

            _started = true;
            
            Server = (BaseServer) gameObject.AddComponent(typeof(TServer));
            _clientImplementationType = typeof(TClient);

            return TryStartServer(suggestedPort, maxClientCount);
        }
        
        private bool TryStartServer(ushort suggestedPort, ushort maxClientCount)
        {
            var port = suggestedPort;
            
            Exception lastException = null;
            for (var i = 0; i < 50; i++)
            {
                try
                {
                    Server.StartServer(port, maxClientCount);
                    return true;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    port++;
                }
            }

            Debug.LogError(lastException);
            return false;
        }
        
        public async Task<(bool success, BaseClient client)> TryConnectAsync(
            string hostAddress, 
            ushort hostPort)
        {
            var client = (BaseClient)gameObject.AddComponent(_clientImplementationType);
            if(!await client.ConnectAsync(hostAddress, hostPort))
            {
                Destroy(client);
                return (false, null);
            }

            _allClients.Add(client);
            return (true, client);
        }

        public void Stop()
        {
            if (Server == null)
            {
                return;
            }

            Server.StopServer();
            Destroy(Server);

            foreach (var client in _allClients)
            {
                client.Disconnect();
                Destroy(client);
            }
        }
    }
}