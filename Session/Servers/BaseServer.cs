﻿using System.Collections.Generic;
using Rip2p.Session.Servers.Connections;
using Riptide;
using UnityEngine;

namespace Rip2p.Session.Servers
{
    public delegate void ClientConnectedDelegate(BaseConnection connection);
    public delegate void ClientDisconnectedDelegate(BaseConnection connection);
    public delegate void MessageReceivedDelegate(BaseConnection connection, ushort messageId, Message message);
    
    public abstract class BaseServer : MonoBehaviour
    {
        [SerializeField] private ushort _port;
        [SerializeField] private ushort _maxClientCount;

        public event ClientConnectedDelegate ClientConnected;
        public event ClientDisconnectedDelegate ClientDisconnected;
        public event MessageReceivedDelegate MessageReceived;
        
        public ushort Port => _port;
        public ushort MaxClientCount => _maxClientCount;

        private readonly Dictionary<ushort, BaseConnection> _clients = new();
        public IReadOnlyDictionary<ushort, BaseConnection> Clients => _clients;

        public void StartServer(ushort port, ushort maxClientCount)
        {
            _port = port;
            _maxClientCount = maxClientCount;
            StartServerInternal(port, maxClientCount);
        }
        
        protected abstract void StartServerInternal(ushort port, ushort maxClientCount);
        
        public abstract void StopServer();

        public abstract void Tick();
        
        protected void OnClientConnected(BaseConnection connection)
        {
            _clients[connection.Id] = connection;
            ClientConnected?.Invoke(connection);
        }

        protected void OnClientDisconnected(BaseConnection connection)
        {
            _clients.Remove(connection.Id);
            ClientDisconnected?.Invoke(connection);
        }

        protected void OnMessageReceived(BaseConnection connection, ushort messageId, Message message)
        {
            MessageReceived?.Invoke(connection, messageId, message);
        }

        public abstract void Send(Message message, ushort clientId);
        public abstract void SendToAllExcept(Message message, ushort client);
    }
}