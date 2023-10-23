using System;
using UnityEngine;

namespace Rip2p.Util
{
    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
    {
        public static T Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStaticMembers()
        {
            Instance = default;
        }

        protected SingletonMonoBehaviour()
        {
            if (Instance != null)
            {
                throw new Exception($"{GetType().FullName} has already been instantiated");
            }
            Instance = (T)this;
        }
    }
}