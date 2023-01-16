using System.Threading.Tasks;
using Riptide;
using UnityEngine;

namespace Rip2p.Clients
{
    public delegate void DisconnectedDelegate();
    public delegate void MessageReceivedDelegate(ushort messageId, Message message);

    public abstract class BaseClient : MonoBehaviour
    {
        [SerializeField] private string _serverAddress;
        [SerializeField] private ushort _serverPort;

        public event DisconnectedDelegate Disconnected;
        public event MessageReceivedDelegate MessageReceived;

        public string ServerAddress => _serverAddress;
        public ushort ServerPort => _serverPort;
        public abstract ushort Id { get; }
        
        public async Task<bool> ConnectAsync(string address, ushort port)
        {
            _serverAddress = address;
            _serverPort = port;
            return await ConnectInternalAsync(address, port);
        }

        protected abstract Task<bool> ConnectInternalAsync(string address, ushort port);

        protected void OnDisconnected()
        {
            Disconnected?.Invoke();
        }
        
        public abstract void Disconnect();

        protected void OnMessageReceived(ushort messageId, Message message)
        {
            MessageReceived?.Invoke(messageId, message);
        }
        
        public abstract void Send(Message message);
    }
}