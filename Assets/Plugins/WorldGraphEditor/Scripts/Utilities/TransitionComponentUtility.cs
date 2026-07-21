using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldGraphEditor
{
    public static class TransitionComponentUtility
    {
        public static ITransitionComponent FindAny()
        {
            return Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<ITransitionComponent>()
                .FirstOrDefault();
        }

        public static T FindAny<T>() where T : MonoBehaviour, ITransitionComponent
        {
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None).FirstOrDefault();
        }
        
        public static ITransitionComponent FindByGuid(string searchedGuid)
        {
            var components = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ITransitionComponent>();
            return FindObjectInternal(searchedGuid, components);
        }

        public static T FindByGuid<T>(string searchedGuid) where T : MonoBehaviour, ITransitionComponent
        {
            var components = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            return FindObjectInternal(searchedGuid, components);
        }

        private static T FindObjectInternal<T>(string searchedGuid, IEnumerable<T> components) where T : ITransitionComponent
        {
#if UNITY_EDITOR
            var context = new RefreshContext(WGEProjectConfig.Instance);
#endif
            foreach (var component in components)
            {
#if UNITY_EDITOR
                component.Refresh(context);
#endif
                if (component.GetGuid() != searchedGuid) 
                    continue;

                return component;
            }
            
            return default;
        }
    }
}