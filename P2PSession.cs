using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rip2p.Clients;
using Rip2p.Peers;
using Rip2p.Servers;
using Rip2p.Servers.Connections;
using Riptide;
using UnityEngine;

namespace Rip2p
{
    public class P2PSession : MonoBehaviour
    {
        private const string LoopbackAddress = "127.0.0.1";
        
        [SerializeField] private bool _isHost;

        public bool IsHost => _isHost;
        
        public BaseServer Server { get; private set; }
        public BaseClient HostClient { get; private set; }
        public IEnumerable<BaseClient> ClientsExceptHostLoopback => _clientsExceptHostLoopback;
        public IEnumerable<BaseClient> AllClients => _allClients;

        private readonly List<BaseClient> _clientsExceptHostLoopback = new();
        private readonly List<BaseClient> _allClients = new();

        // TODO: Something like this?
        // private readonly Dictionary<ushort, IPeer> _peerIdsToPeer = new();
        // private readonly Dictionary<ushort, ushort> _serverConnectionsToPeerIds = new();
        // private readonly Dictionary<ushort, ushort> _clientIdsToPeerIds = new();
        
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
            
            Server = (BaseServer) gameObject.AddComponent(typeof(TServer));
            
            Server.ClientConnected += OnServerClientConnected;
            Server.ClientDisconnected += OnServerClientDisconnected;
            Server.MessageReceived += OnServerMessageReceived;
            
            if (!TryStartServer(suggestedPort, maxClientCount))
            {
                return false;
            }

            if (hostAddress == null)
            {
                _isHost = true;
                hostAddress = LoopbackAddress;
                hostPort = Server.Port;
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
            
            client.Disconnected += OnClientDisconnected;
            client.MessageReceived += OnClientMessageReceived;

            if (hostAddress == LoopbackAddress)
            {
                client.IsHostLoopbackClient = true;
            }
            else
            {
                _clientsExceptHostLoopback.Add(client);
            }

            _allClients.Add(client);

            if (await client.ConnectAsync(hostAddress, hostPort))
            {
                return (true, client);
            }

            RemoveClient(client);
            return (false, null);
        }

        private void RemoveClient(BaseClient client)
        {
            client.Disconnect();
            client.Disconnected -= OnClientDisconnected;
            client.MessageReceived -= OnClientMessageReceived;

            _clientsExceptHostLoopback.Remove(client);
            _allClients.Remove(client);
            Destroy(client);
        }

        private void OnServerClientConnected(BaseConnection connection)
        {
            
        }
        
        private void OnServerClientDisconnected(BaseConnection connection)
        {
            
        }
        
        private void OnServerMessageReceived(BaseConnection connection, ushort messageId, Message message)
        {
            
        }
        
        private void OnClientDisconnected(BaseClient client)
        {
            
        }
        
        private void OnClientMessageReceived(BaseClient client, ushort messageId, Message message)
        {
            
        }

        public void Send(Message message, IPeer peer)
        {
            peer.Send(message);
        }

        public void SendToOthers(Message message, bool returnMessageToPool = true)
        {
            if (IsHost)
            {
                Server.SendToAllExcept(message, HostClient.Id);
            }
            else
            {
                Server.SendToAll(message);
            }
            
            foreach (var client in _clientsExceptHostLoopback)
            {
                client.Send(message);
            }

            if (returnMessageToPool)
            {
                message.Release();
            }
        }

        public void SendToHost(Message message, bool returnMessageToPool = true)
        {
            HostClient.Send(message);
            
            if (returnMessageToPool)
            {
                message.Release();
            }
        }

        public void Stop()
        {
            if (Server == null)
            {
                return;
            }

            Server.StopServer();
            Server.ClientConnected -= OnServerClientConnected;
            Server.ClientDisconnected -= OnServerClientDisconnected;
            Server.MessageReceived -= OnServerMessageReceived;
            Destroy(Server);

            foreach (var client in _allClients.ToArray())
            {
                RemoveClient(client);
            }
        }
    }
}