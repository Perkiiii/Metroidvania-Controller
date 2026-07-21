using System;
using System.Threading.Tasks;
using UnityEngine;

namespace WorldGraphEditor
{
    public partial class TransitionManager
    {
        [Obsolete("Use NextSpawnPosition instead.")]
        public Vector3 PlayerSpawnPosition => NextSpawnPosition;
        
        [Obsolete("Use LoadSceneAsync instead.")]
        public Task LoadScene(int buildIndex, TransitionContext context = null)
        {
            return LoadSceneAsync(buildIndex, context);
        }

        [Obsolete("Use GoToAsync instead.")]
        public async void GoTo(string currentPortGuid, string targetPortGuid, ITransitionContext context = null)
        {
            await GoToAsync(currentPortGuid, targetPortGuid, context);
        }
        
        [Obsolete("Use GoFromAsync instead.")]
        public async void GoFrom(string currentPortGuid, bool ignoreShortcuts, ITransitionContext context = null)
        {
            await GoFromAsync(currentPortGuid, ignoreShortcuts, context);
        }

        [Obsolete("Use Instance.Initialized instead.")]
        public static event Action OnInitialized
        {
            add => Instance.Initialized += value;
            remove
            {
                if (Instance != null) 
                    Instance.Initialized -= value;
            }
        }

        [Obsolete("Use Instance.PortEntered instead.")]
        public static event Action OnPortEntered
        {
            add => Instance.PortEntered += value;
            remove
            {
                if (Instance != null) 
                    Instance.PortEntered -= value;
            }
        }

        [Obsolete("Use Instance.TransitionStarted instead.")]
        public static event Action OnTransitionStarted
        {
            add => Instance.TransitionStarted += value;
            remove
            {
                if (Instance != null)
                    Instance.TransitionStarted -= value;
            }
        }

        [Obsolete("Use Instance.SceneLoaded instead.")]
        public static event Action OnSceneLoaded
        {
            add => Instance.SceneLoaded += value;
            remove
            {
                if (Instance != null)
                    Instance.SceneLoaded -= value;
            }
        }

        [Obsolete("Use Instance.TransitionEnded instead.")]
        public static event Action OnTransitionEnded
        {
            add => Instance.TransitionEnded += value;
            remove
            {
                if (Instance != null)
                    Instance.TransitionEnded -= value;
            }
        }

        [Obsolete("Use Instance.PortLeaved instead.")]
        public static event Action OnPortLeaved
        {
            add => Instance.PortLeaved += value;
            remove
            {
                if (Instance != null)
                    Instance.PortLeaved -= value;
            }
        }

        [Obsolete("Use Instance.Destroyed instead.")]
        public static event Action OnDestroyed
        {
            add => Instance.Destroyed += value;
            remove
            {
                if (Instance != null)
                    Instance.Destroyed -= value;
            }
        }
    }
}