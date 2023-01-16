using System;
using System.Threading.Tasks;
using Rip2p.Clients;
using Rip2p.Servers;
using Rip2p.Servers.Connections;
using Riptide;
using UnityEngine;

namespace Rip2p
{
    public class P2PSession : MonoBehaviour
    {
        [SerializeField] private bool _isHost;

        public bool IsHost => _isHost;
        
        private BaseServer _server;
        private BaseClient _client;

        private bool _hasStarted;
        
        public async Task<bool> TryStartAsync<TServer, TClient>(
            ushort suggestedPort, 
            ushort maxClientCount)
            where TServer : BaseServer 
            where TClient : BaseClient
        {
            if (_hasStarted)
            {
                return false;
            }
            _hasStarted = true;
            
            _isHost = true;
            
            _server = (BaseServer) gameObject.AddComponent(typeof(TServer));
            
            _server.ClientConnected += OnServerClientConnected;
            _server.ClientDisconnected += OnServerClientDisconnected;
            _server.MessageReceived += OnServerMessageReceived;
            
            if (!TryStartServer(suggestedPort, maxClientCount))
            {
                return false;
            }
            
            return await TryConnectAsync<TClient>("127.0.0.1", _server.Port);
        }
        
        public async Task<bool> TryStartAsync<TClient>(
            string hostAddress,
            ushort hostPort) 
            where TClient : BaseClient
        {
            if (_hasStarted)
            {
                return false;
            }
            _hasStarted = true;
            
            return await TryConnectAsync<TClient>(hostAddress, hostPort);
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
        
        private async Task<bool> TryConnectAsync<TClient>(
            string hostAddress,
            ushort hostPort) where TClient : BaseClient
        {
            _client = gameObject.AddComponent<TClient>();
            
            _client.Disconnected += OnClientDisconnected;
            _client.MessageReceived += OnClientMessageReceived;

            return await _client.ConnectAsync(hostAddress, hostPort);
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

        public void SendToServer(
            MessageSendMode sendMode, 
            Enum messageId, 
            Action<Message> addToMessage)
        {
            var message = CreateMessage(
                sendMode,
                MessageRecipient.Server,
                messageId,
                addToMessage);

            if (IsHost)
            {
                OnServerMessageReceived(
                    _server.Clients[_client.Id], 
                    Convert.ToUInt16(messageId), 
                    message);
            }
            else
            {
                _client.Send(message);
            }

            message.Release();
        }
        
        public void SendToClient(
            MessageSendMode sendMode, 
            Enum messageId, 
            Action<Message> addToMessage, 
            ushort clientId)
        {
            var message = CreateMessage(
                sendMode,
                MessageRecipient.SpecificClient,
                messageId,
                addToMessage,
                clientId);

            if (IsHost)
            {
                _server.Send(message, clientId);
            }
            else
            {
                _client.Send(message);
            }
            
            message.Release();
        }
        
        public void SendToOthers(
            MessageSendMode sendMode, 
            Enum messageId, 
            Action<Message> addToMessage)
        {
            var message = CreateMessage(
                sendMode,
                MessageRecipient.OtherClients,
                messageId,
                addToMessage);
            
            if (IsHost)
            {
                _server.SendToAllExcept(message, _client.Id);
            }
            else
            {
                _client.Send(message);
            }
            
            message.Release();
        }

        public void SendToAllClientsIncludingMyself(
            MessageSendMode sendMode, 
            Enum messageId, 
            Action<Message> addToMessage)
        {
            var message = CreateMessage(
                sendMode,
                MessageRecipient.AllClientsIncludingMyself,
                messageId,
                addToMessage);

            if (IsHost)
            {
                _server.SendToAll(message);
            }
            else
            {
                _client.Send(message);
            }
            
            message.Release();
        }

        private Message CreateMessage(
            MessageSendMode sendMode, 
            MessageRecipient recipient,
            Enum messageId, 
            Action<Message> addToMessage,
            ushort specificClientId = 0)
        {
            var message = Message.Create(sendMode, messageId);
            
            var recipientWord = (ushort)recipient;
            if (recipient == MessageRecipient.SpecificClient)
            {
                recipientWord |= (ushort)(specificClientId << 2);
            }
            message.AddUShort(recipientWord);
            
            addToMessage(message);

            return message;
        }

        public void Stop()
        {
            if (_server != null)
            {
                _server.StopServer();
                _server.ClientConnected -= OnServerClientConnected;
                _server.ClientDisconnected -= OnServerClientDisconnected;
                _server.MessageReceived -= OnServerMessageReceived;
                Destroy(_server);
            }

            if (_client != null)
            {
                _client.Disconnect();
                _client.Disconnected -= OnClientDisconnected;
                _client.MessageReceived -= OnClientMessageReceived;
                Destroy(_client);
            }
        }

        enum MessageRecipient
        {
            Server,
            SpecificClient,
            OtherClients,
            AllClientsIncludingMyself
        }
    }
}