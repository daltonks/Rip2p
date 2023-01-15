using UnityEngine;

namespace Rip2p.Servers
{
    public abstract class BaseServer : MonoBehaviour
    {
        [SerializeField] private ushort _port;
        [SerializeField] private ushort _maxClientCount;

        public ushort Port => _port;
        public ushort MaxClientCount => _maxClientCount;

        public void StartServer(ushort port, ushort maxClientCount)
        {
            _port = port;
            _maxClientCount = maxClientCount;
            StartServerInternal(port, maxClientCount);
        }

        protected abstract void StartServerInternal(ushort port, ushort maxClientCount);
        
        public abstract void StopServer();
    }
}