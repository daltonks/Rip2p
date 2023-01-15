using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Rip2p.Clients
{
    public abstract class BaseClient : MonoBehaviour
    {
        [SerializeField] private bool _isHostClient;
        [SerializeField] private string _address;
        [SerializeField] private ushort _port;

        public event Action Disconnected;
        
        public async Task<bool> ConnectAsync(bool isHostClient, string address, ushort port)
        {
            _isHostClient = isHostClient;
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