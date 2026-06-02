using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldGraphEditor
{
    public class ObjectUtility
    {
        public static IEnumerable<T> FindObjectsByInterface<T>()
        {
            return Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<T>();
        }
    }
}