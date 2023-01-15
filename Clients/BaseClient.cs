using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Rip2p.Clients
{
    public abstract class BaseClient : MonoBehaviour
    {
        [SerializeField] internal bool _isHostClient;
        [SerializeField] private string _address;
        [SerializeField] private ushort _port;

        public event Action Disconnected;

        public string Address => _address;
        public ushort Port => _port;
        
        public async Task<bool> ConnectAsync(string address, ushort port)
        {
            _address = address;
            _port = port;
            return await ConnectInternalAsync(address, port);
        }

        protected abstract Task<bool> ConnectInternalAsync(string address, ushort port);

        protected void OnDisconnected()
        {
            Disconnected?.Invoke();
        }
        
        public abstract void Disconnect();
    }
}