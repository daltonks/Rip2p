using System;
using System.Threading.Tasks;
using Rip2p.Session.Clients;
using Rip2p.Session.Servers;
using Rip2p.Session.Servers.Connections;
using Riptide;
using UnityEngine;

namespace Rip2p.Session
{
    public abstract class P2PSession<TMessageType> : MonoBehaviour where TMessageType : Enum
    {
        [SerializeField] private bool _isHost;
        
        public bool IsHost => _isHost;
        public ushort ClientId => _client.Id;
        
        private BaseServer _server;
        private BaseClient _client;

        private bool _hasStarted;

        public async Task<(bool success, string message)> TryStartAsync<TServer, TClient>(
            ushort suggestedPort, 
            ushort maxClientCount)
            where TServer : BaseServer 
            where TClient : BaseClient
        {
            if (_hasStarted)
            {
                return (false, "Session has already started");
            }
            _hasStarted = true;
            
            _isHost = true;

            _server = (BaseServer) gameObject.AddComponent(typeof(TServer));
            
            _server.ClientConnected += OnServerClientConnected;
            _server.ClientDisconnected += OnServerClientDisconnected;
            _server.MessageReceived += OnServerMessageReceived;
            
            if (!TryStartServer(suggestedPort, maxClientCount))
            {
                return (false, "Unable to start server");
            }
            
            return await TryConnectAsync<TClient>("127.0.0.1", _server.Port);
        }
        
        public async Task<(bool success, string message)> TryStartAsync<TClient>(
            string hostAddress,
            ushort hostPort) 
            where TClient : BaseClient
        {
            if (_hasStarted)
            {
                return (false, "Session has already started");
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
        
        private async Task<(bool success, string message)> TryConnectAsync<TClient>(
            string hostAddress,
            ushort hostPort) where TClient : BaseClient
        {
            _client = gameObject.AddComponent<TClient>();

            _client.ClientConnected += OnMyClientConnected;
            _client.OtherClientConnected += OnOtherClientConnected;
            _client.Disconnected += OnMyClientDisconnected;
            _client.OtherClientDisconnected += OnOtherClientDisconnected;
            _client.MessageReceived += OnClientMessageReceived;

            return await _client.ConnectAsync(hostAddress, hostPort);
        }

        protected virtual void FixedUpdate()
        {
            Tick();
            _client.Tick();
            if (IsHost)
            {
                _server.Tick();
            }
        }

        protected abstract void Tick();

        protected abstract void OnServerClientConnected(BaseConnection connection);
        protected abstract void OnMyClientConnected(ushort clientId);
        protected abstract void OnOtherClientConnected(ushort clientId);
        protected abstract void OnServerClientDisconnected(BaseConnection connection);
        protected abstract void OnMyClientDisconnected(ushort clientId);
        protected abstract void OnOtherClientDisconnected(ushort clientId);
        protected abstract void OnServerMessageReceived(BaseConnection connection, TMessageType messageType, Message message);
        protected abstract void OnClientMessageReceived(TMessageType messageType, Message message);
        
        private void OnServerMessageReceived(BaseConnection connection, ushort messageType, Message message)
        {
            var (recipient, clientId) = ReadMessageRecipient(message);
            switch (recipient)
            {
                case MessageRecipient.Server:
                    OnServerMessageReceived(
                        connection, 
                        (TMessageType) Enum.ToObject(typeof(TMessageType), messageType), 
                        message);
                    break;
                case MessageRecipient.SpecificClient:
                    _server.Send(message, clientId);
                    break;
                case MessageRecipient.ExceptClient:
                    _server.SendToAllExcept(message, clientId);
                    break;
                case MessageRecipient.OtherClients:
                    _server.SendToAllExcept(message, connection.Id);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private void OnClientMessageReceived(ushort messageType, Message message)
        {
            _ = ReadMessageRecipient(message);
            OnClientMessageReceived(
                (TMessageType) Enum.ToObject(typeof(TMessageType), messageType), 
                message);
        }
        
        public void SendToServer(
            MessageSendMode sendMode, 
            TMessageType messageType, 
            Action<Message> addToMessage)
        {
            var message = CreateMessage(
                sendMode,
                MessageRecipient.Server,
                messageType,
                addToMessage);

            _client.Send(message);

            message.Release();
        }
        
        public void SendToClient(
            MessageSendMode sendMode, 
            TMessageType messageType, 
            Action<Message> addToMessage, 
            ushort clientId)
        {
            var message = CreateMessage(
                sendMode,
                MessageRecipient.SpecificClient,
                messageType,
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
            TMessageType messageType, 
            Action<Message> addToMessage)
        {
            var message = CreateMessage(
                sendMode,
                MessageRecipient.OtherClients,
                messageType,
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

        public void SendToAllClientsExcept(
            MessageSendMode sendMode, 
            TMessageType messageType, 
            Action<Message> addToMessage, 
            ushort clientId)
        {
            var message = CreateMessage(
                sendMode,
                MessageRecipient.ExceptClient,
                messageType,
                addToMessage,
                clientId);

            if (IsHost)
            {
                _server.SendToAllExcept(message, clientId);
            }
            else
            {
                _client.Send(message);
            }
            
            message.Release();
        }
        
        public void SendToAllClientsIncludingMyself(
            MessageSendMode sendMode, 
            TMessageType messageType, 
            Action<Message> addToMessage)
        {
            var message = CreateMessage(
                sendMode,
                MessageRecipient.OtherClients,
                messageType,
                addToMessage);

            // Read past the messageId
            _ = message.GetUShort();
            OnClientMessageReceived((ushort) (object) messageType, message);
            
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
            TMessageType messageType, 
            Action<Message> addToMessage,
            ushort clientId = 0)
        {
            var message = Message.Create(sendMode, messageType);
            
            var recipientWord = (ushort)((ushort)recipient | (ushort)(clientId << 2));
            message.AddUShort(recipientWord);
            
            addToMessage(message);

            return message;
        }

        private (MessageRecipient recipient, ushort clientId) ReadMessageRecipient(Message message)
        {
            var recipientWord = message.GetUShort();
            var recipient = (MessageRecipient) (recipientWord & 0b11);
            var clientId = (ushort)(recipientWord >> 2);
            return (recipient, clientId);
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
                _client.ClientConnected -= OnMyClientConnected;
                _client.OtherClientConnected -= OnOtherClientConnected;
                _client.Disconnected -= OnMyClientDisconnected;
                _client.OtherClientDisconnected -= OnOtherClientDisconnected;
                _client.MessageReceived -= OnClientMessageReceived;
                Destroy(_client);
            }
        }

        enum MessageRecipient
        {
            Server,
            SpecificClient,
            ExceptClient,
            OtherClients
        }
    }
}