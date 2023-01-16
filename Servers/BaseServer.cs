using System.Collections.Generic;
using Riptide;
using UnityEngine;

namespace Rip2p.Servers
{
    public delegate void ClientConnectedDelegate(ushort client);
    public delegate void ClientDisconnectedDelegate(ushort client);
    public delegate void MessageReceivedDelegate(ushort client, Message message);
    
    public abstract class BaseServer : MonoBehaviour
    {
        [SerializeField] private ushort _port;
        [SerializeField] private ushort _maxClientCount;

        public event ClientConnectedDelegate ClientConnected;
        public event ClientDisconnectedDelegate ClientDisconnected;
        public event MessageReceivedDelegate MessageReceived;
        
        public ushort Port => _port;
        public ushort MaxClientCount => _maxClientCount;

        private readonly HashSet<ushort> _clients = new();
        public IEnumerable<ushort> Clients => _clients;

        public void StartServer(ushort port, ushort maxClientCount)
        {
            _port = port;
            _maxClientCount = maxClientCount;
            StartServerInternal(port, maxClientCount);
        }

        protected abstract void StartServerInternal(ushort port, ushort maxClientCount);
        
        public abstract void StopServer();

        protected void OnClientConnected(ushort client)
        {
            _clients.Add(client);
            ClientConnected?.Invoke(client);
        }

        protected void OnClientDisconnected(ushort client)
        {
            _clients.Remove(client);
            ClientDisconnected?.Invoke(client);
        }

        protected void OnMessageReceived(ushort client, Message message)
        {
            MessageReceived?.Invoke( client, message);
        }

        public abstract void SendToAll(Message message);
        public abstract void SendToAllExcept(Message message, ushort client);
    }
}