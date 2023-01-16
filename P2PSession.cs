using System;
using System.Threading.Tasks;
using Rip2p.Clients;
using Rip2p.Servers;
using Rip2p.Servers.Connections;
using Riptide;
using UnityEngine;

namespace Rip2p
{
    public delegate void ServerClientConnectedDelegate(BaseConnection connection);
    public delegate void ServerClientDisconnectedDelegate(BaseConnection connection);
    public delegate void ServerReceivedMessageDelegate(BaseConnection connection, ushort messageId, Message message);
    public delegate void ClientDisconnectedDelegate();
    public delegate void ClientReceivedMessageDelegate(ushort messageId, Message message);
    
    public class P2PSession : MonoBehaviour
    {
        [SerializeField] private bool _isHost;

        public event ServerClientConnectedDelegate ServerClientConnected;
        public event ServerClientDisconnectedDelegate ServerClientDisconnected;
        public event ServerReceivedMessageDelegate ServerMessageReceived;
        public event ClientDisconnectedDelegate ClientDisconnected;
        public event ClientReceivedMessageDelegate ClientMessageReceived;
        
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
            ServerClientConnected?.Invoke(connection);
        }
        
        private void OnServerClientDisconnected(BaseConnection connection)
        {
            ServerClientDisconnected?.Invoke(connection);
        }
        
        private void OnServerMessageReceived(BaseConnection connection, ushort messageId, Message message)
        {
            var (recipient, specificClientId) = ReadMessageRecipient(message);
            switch (recipient)
            {
                case MessageRecipient.Server:
                    ServerMessageReceived?.Invoke(connection, messageId, message);
                    break;
                case MessageRecipient.SpecificClient:
                    _server.Send(message, specificClientId);
                    break;
                case MessageRecipient.OtherClients:
                    _server.SendToAllExcept(message, connection.Id);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private void OnClientDisconnected()
        {
            ClientDisconnected?.Invoke();
        }
        
        private void OnClientMessageReceived(ushort messageId, Message message)
        {
            _ = ReadMessageRecipient(message);
            ClientMessageReceived?.Invoke(messageId, message);
        }

        public void SendToServer(
            MessageSendMode sendMode, 
            ushort messageId, 
            Action<Message> addToMessage)
        {
            var message = CreateMessage(
                sendMode,
                MessageRecipient.Server,
                messageId,
                addToMessage);

            if (IsHost)
            {
                // Read past messageId
                _ = message.GetUShort();
                OnServerMessageReceived(
                    _server.Clients[_client.Id], 
                    messageId, 
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
            ushort messageId, 
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
        
        public void SendToOtherClients(
            MessageSendMode sendMode, 
            ushort messageId, 
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
            ushort messageId, 
            Action<Message> addToMessage)
        {
            var message = CreateMessage(
                sendMode,
                MessageRecipient.OtherClients,
                messageId,
                addToMessage);

            // Read past messageId
            _ = message.GetUShort();
            OnClientMessageReceived(messageId, message);
            
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

        private Message CreateMessage(
            MessageSendMode sendMode, 
            MessageRecipient recipient,
            ushort messageId, 
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

        private (MessageRecipient recipient, ushort specificClientId) ReadMessageRecipient(Message message)
        {
            var recipientWord = message.GetUShort();
            var recipient = (MessageRecipient) (recipientWord & 0b11);
            var specificClientId = (ushort)(recipientWord >> 2);
            return (recipient, specificClientId);
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
            OtherClients
        }
    }
}