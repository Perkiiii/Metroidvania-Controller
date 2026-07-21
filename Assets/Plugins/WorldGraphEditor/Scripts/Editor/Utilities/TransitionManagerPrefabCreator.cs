using System.IO;
using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal static class TransitionManagerPrefabCreator
    {
        private static readonly string _prefabPath =
            Path.Combine("Assets", "WorldGraphEditor", "Resources", "TransitionManager.prefab");

        [MenuItem("Tools/World Graph Editor/Create Transition Manager Prefab")]
        public static void CreateOrOpenPrefab()
        {
            var manager = TransitionManager.LoadFromResources();
            
            if (manager != null)
            {
                Selection.activeObject = manager;
                EditorGUIUtility.PingObject(manager);
                return;
            }

            var go = new GameObject("TransitionManager");
            go.AddComponent<TransitionManager>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, _prefabPath);
            
            if (prefab != null)
            {
                Debug.Log($"TransitionManager prefab created at \"{_prefabPath}\".");
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            
            Object.DestroyImmediate(go);
        }
    }
}