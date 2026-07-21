using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    public static class ObjectUtility
    {
        public static IEnumerable<T> FindObjectsByInterface<T>()
        {
#if UNITY_6000_5_OR_NEWER
            return Object.FindObjectsByType<MonoBehaviour>().OfType<T>();
#else
            return Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<T>();
#endif
        }
    }
}