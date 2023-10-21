using Riptide;
using UnityEngine;

namespace Rip2p
{
    public interface IData
    {
        GameObject GetPrefab();
        void WriteTo(Message message);
        void ReadFrom(Message message);
    }
}