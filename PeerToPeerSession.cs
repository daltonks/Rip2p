using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rip2p.Clients;
using Rip2p.Servers;
using UnityEngine;

namespace Rip2p
{
    public class PeerToPeerSession : MonoBehaviour
    {
        private BaseServer _server;
        
        private BaseClient _hostClient;
        private readonly List<BaseClient> _allClients = new();

        private Type _clientImplementationType;
        
        public void Init<TServer, TClient>() 
            where TServer : BaseServer 
            where TClient : BaseClient
        {
            _server = (BaseServer) gameObject.AddComponent(typeof(TServer));
            
            _clientImplementationType = typeof(TClient);
            _hostClient = AddClient();
        }

        private BaseClient AddClient()
        {
            var client = (BaseClient)gameObject.AddComponent(_clientImplementationType);
            _allClients.Add(client);
            return client;
        }
        
        public (bool success, ushort port) TryStartServer(ushort suggestedPort, ushort maxClientCount)
        {
            var port = suggestedPort;
            
            Exception lastException = null;
            for (var i = 0; i < 50; i++)
            {
                try
                {
                    _server.StartServer(port, maxClientCount);
                    return (true, port);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    port++;
                }
            }

            Debug.LogError(lastException);
            return (false, 0);
        }

        public async Task<bool> TryConnectToHostAsync(string hostAddress, ushort hostPort)
        {
            return await _hostClient.ConnectAsync(isHostClient: true, hostAddress, hostPort);
        }

        public void StopSession()
        {
            if (_server == null)
            {
                return;
            }

            _server.StopServer();
            Destroy(_server);

            foreach (var client in _allClients)
            {
                client.Disconnect();
                Destroy(client);
            }
        }
    }
}