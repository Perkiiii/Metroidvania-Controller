using System;
using System.Threading.Tasks;
using UnityEngine;

namespace WorldGraphEditor
{
    public interface ITransitionManager
    {
        public IWorldGraph Graph { get; }
        
        public Vector3 NextSpawnPosition { get; }

        public ITransitionComponent OutputTransitionComponent { get; }
        public ITransitionComponent InputTransitionComponent { get; }

        public event Action TransitionStarted;
        public event Action SceneLoaded;
        public event Action TransitionEnded;

        public Task GoToAsync(string currentPortGuid, string targetPortGuid, ITransitionContext context = null);
        public Task GoFromAsync(string currentPortGuid, ITransitionContext context = null);
    }
}