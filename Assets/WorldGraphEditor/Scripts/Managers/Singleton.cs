using System;
using UnityEngine;

namespace WorldGraphEditor
{
    public abstract class StaticInstance<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; protected set; }

        public event Action OnManagerDisabled;
        
        protected virtual void Awake() => Instance = this as T;

        protected virtual void OnApplicationQuit()
        {
            OnManagerDisabled?.Invoke();
            Instance = null;
            Destroy(gameObject);
        }
    }
    
    public abstract class Singleton<T> : StaticInstance<T> where T : MonoBehaviour
    {
        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            base.Awake();
        }
    }
    
    public abstract class PersistentSingleton<T> : Singleton<T> where T : MonoBehaviour
    {
        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }
    }
}