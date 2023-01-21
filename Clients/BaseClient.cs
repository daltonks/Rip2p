using System.Threading.Tasks;
using Riptide;
using UnityEngine;

namespace Rip2p.Clients
{
    public delegate void DisconnectedDelegate();
    public delegate void MessageReceivedDelegate(ushort messageId, Message message);

    public abstract class BaseClient : MonoBehaviour
    {
        [SerializeField] protected string _serverAddress;
        [SerializeField] protected ushort _serverPort;

        public event DisconnectedDelegate Disconnected;
        public event MessageReceivedDelegate MessageReceived;

        public string ServerAddress => _serverAddress;
        public ushort ServerPort => _serverPort;
        public abstract ushort Id { get; }
        
        public async Task<(bool success, string message)> ConnectAsync(string address, ushort port)
        {
            _serverAddress = address;
            _serverPort = port;
            return await ConnectInternalAsync(address, port);
        }

        protected abstract Task<(bool success, string message)> ConnectInternalAsync(string address, ushort port);

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