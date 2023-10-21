using Riptide;
using UnityEngine;

namespace Rip2p.Session.Data
{
    public interface INetworkData
    {
        GameObject GetPrefab();
        void WriteTo(Message message);
        void ReadFrom(Message message);
    }
}