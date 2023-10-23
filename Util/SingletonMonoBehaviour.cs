using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Rip2p.Util
{
    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
    {
        protected SingletonMonoBehaviour()
        {
            if (Instance != null)
            {
                throw new Exception($"{GetType().FullName} has already been instantiated");
            }
            Instance = (T)this;
        }
        
        public static T Instance { get; private set; }

        private void OnDestroy()
        {
            Instance = null;
        }
    }
}