using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace Rip2p.Util
{
    public class MessagingService
    {
        public static MessagingService Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStaticMembers()
        {
            Instance = new MessagingService();
        }
        
        public delegate void EventDelegate<in T>(T message);
        private delegate void EventDelegate(object message);
	
        private readonly ConcurrentDictionary<Type, EventDelegate> _typeToMultiDelegateMap = new();
        private readonly Dictionary<Delegate, EventDelegate> _delegateToWrappingMap = new();
	
        public void AddListener<T>(EventDelegate<T> del)
        {
            lock (this)
            {
                // Early-out if we've already registered this delegate
                if (_delegateToWrappingMap.ContainsKey(del))
                    return;
		
                // Create a new non-generic delegate which calls our generic one.
                // This is the delegate we actually invoke.
                _delegateToWrappingMap[del] = WrapDelegate;

                if (_typeToMultiDelegateMap.ContainsKey(typeof(T)))
                {
                    _typeToMultiDelegateMap[typeof(T)] += WrapDelegate; 
                }
                else
                {
                    _typeToMultiDelegateMap[typeof(T)] = WrapDelegate;
                }
            }

            void WrapDelegate(object e) => del((T) e);
        }
	
        public void RemoveListener<T>(EventDelegate<T> del)
        {
            lock (this)
            {
                if (_delegateToWrappingMap.TryGetValue(del, out var wrappingDelegate))
                {
                    if (_typeToMultiDelegateMap.TryGetValue(typeof(T), out var multiDelegate))
                    {
                        // ReSharper disable once DelegateSubtraction
                        multiDelegate -= wrappingDelegate;
                        if (multiDelegate == null)
                        {
                            _typeToMultiDelegateMap.TryRemove(typeof(T), out _);
                        }
                        else
                        {
                            _typeToMultiDelegateMap[typeof(T)] = multiDelegate;
                        }
                    }
			
                    _delegateToWrappingMap.Remove(del);
                }
            }
        }
	
        public void Raise(object message)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (_typeToMultiDelegateMap.TryGetValue(message.GetType(), out var multiDelegate))
            {
                multiDelegate?.Invoke(message);
            }
        }
    }
}
