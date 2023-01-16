using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rip2p.Clients;
using Rip2p.Servers;
using Riptide;
using UnityEngine;

namespace Rip2p
{
    public class P2PSession : MonoBehaviour
    {
        private const string LoopbackAddress = "127.0.0.1";
        
        [SerializeField] private bool _isHost;

        public bool IsHost => _isHost;
        
        public BaseClient HostClient { get; private set; }
        
        private BaseServer _server;
        private readonly List<BaseClient> _clientsExceptHostLoopback = new();
        private readonly List<BaseClient> _allClients = new();

        private Type _clientImplementationType;
        
        private bool _started;
        public async Task<bool> TryStartAsync<TServer, TClient>(
            ushort suggestedPort, 
            ushort maxClientCount,
            string hostAddress = null,
            ushort hostPort = 0)
            where TServer : BaseServer 
            where TClient : BaseClient
        {
            if (_started)
            {
                return false;
            }

            _started = true;
            
            _clientImplementationType = typeof(TClient);
            
            _server = (BaseServer) gameObject.AddComponent(typeof(TServer));
            
            _server.ClientConnected += OnServerClientConnected;
            _server.ClientDisconnected += OnServerClientDisconnected;
            _server.MessageReceived += OnServerMessageReceived;
            
            if (!TryStartServer(suggestedPort, maxClientCount))
            {
                return false;
            }

            if (hostAddress == null)
            {
                _isHost = true;
                hostAddress = LoopbackAddress;
                hostPort = _server.Port;
            }
            
            var (connectSuccess, client) = await TryConnectAsync(hostAddress, hostPort);
            HostClient = client;
            return connectSuccess;
        }

        private bool TryStartServer(ushort suggestedPort, ushort maxClientCount)
        {
            var port = suggestedPort;
            
            Exception lastException = null;
            for (var i = 0; i < 50; i++)
            {
                try
                {
                    _server.StartServer(port, maxClientCount);
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
            
            client.Disconnected += OnClientDisconnected;
            client.MessageReceived += OnMessageReceived;

            if (hostAddress == LoopbackAddress)
            {
                client.IsHostLoopbackClient = true;
            }
            else
            {
                _clientsExceptHostLoopback.Add(client);
            }

            _allClients.Add(client);
            
            if(!await client.ConnectAsync(hostAddress, hostPort))
            {
                RemoveClient(client);
                return (false, null);
            }

            return (true, client);
        }

        private void RemoveClient(BaseClient client)
        {
            client.Disconnected -= OnClientDisconnected;
            client.MessageReceived -= OnMessageReceived;

            _clientsExceptHostLoopback.Remove(client);
            _allClients.Remove(client);
            Destroy(client);
        }

        private void OnServerMessageReceived(ushort client, Message message)
        {
            
        }
        
        private void OnServerClientConnected(ushort client)
        {
            
        }
        
        private void OnServerClientDisconnected(ushort client)
        {
            
        }
        
        private void OnMessageReceived(BaseClient client, Message message)
        {
            
        }
        
        private void OnClientDisconnected(BaseClient client)
        {
            
        }

        public void SendToOthers(Message message, bool returnToPool = true)
        {
            if (IsHost)
            {
                _server.SendToAllExcept(message, HostClient.Id);
            }
            else
            {
                _server.SendToAll(message);
            }
            
            foreach (var client in _clientsExceptHostLoopback)
            {
                client.Send(message);
            }

            if (returnToPool)
            {
                message.Release();
            }
        }

        public void SendToHost(Message message, bool returnToPool = true)
        {
            HostClient.Send(message);
            
            if (returnToPool)
            {
                message.Release();
            }
        }

        public void Stop()
        {
            if (_server == null)
            {
                return;
            }

            _server.StopServer();
            _server.ClientConnected -= OnServerClientConnected;
            _server.ClientDisconnected -= OnServerClientDisconnected;
            _server.MessageReceived -= OnServerMessageReceived;
            Destroy(_server);

            foreach (var client in _allClients.ToArray())
            {
                RemoveClient(client);
            }
        }
    }
}