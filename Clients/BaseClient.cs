using System.Threading.Tasks;
using Riptide;
using UnityEngine;

namespace Rip2p.Clients
{
    public delegate void DisconnectedDelegate(BaseClient client);
    public delegate void MessageReceivedDelegate(BaseClient client, Message message);

    public abstract class BaseClient : MonoBehaviour
    {
        [SerializeField] private bool _isHostLoopbackClient;
        [SerializeField] private string _address;
        [SerializeField] private ushort _port;

        public event DisconnectedDelegate Disconnected;
        public event MessageReceivedDelegate MessageReceived;

        public bool IsHostLoopbackClient
        {
            get => _isHostLoopbackClient;
            internal set => _isHostLoopbackClient = value;
        }
        public string Address => _address;
        public ushort Port => _port;
        public abstract ushort Id { get; }
        
        public async Task<bool> ConnectAsync(string address, ushort port)
        {
            _address = address;
            _port = port;
            return await ConnectInternalAsync(address, port);
        }

        protected abstract Task<bool> ConnectInternalAsync(string address, ushort port);

        protected void OnDisconnected()
        {
            Disconnected?.Invoke(this);
        }
        
        public abstract void Disconnect();

        protected void OnMessageReceived(Message message)
        {
            MessageReceived?.Invoke(this, message);
        }
        
        public abstract void Send(Message message);
    }
}