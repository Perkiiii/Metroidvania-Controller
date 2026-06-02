using System.IO;
using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal abstract class ScriptableSingleton<T> : ScriptableObject where T : ScriptableSingleton<T>
    {
        public virtual string FolderPath => "Assets/WorldGraphEditor/Editor/Settings";
        public virtual string FileName => typeof(T).Name;

        private string _assetPath => Path.Combine(FolderPath, $"{FileName}.asset");
        
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;
                
                _instance = FindExistingInstance();

                if (_instance != null)
                    return _instance;
                
                _instance = CreateNewInstance();
                WGEConsole.Log($"[{typeof(T).Name}] Created new asset at: {_instance._assetPath}");

                return _instance;
            }
        }

        private static T CreateNewInstance()
        {
            var instance = CreateInstance<T>();
            var folderPath = instance.FolderPath;

            if (!AssetDatabase.IsValidFolder(folderPath))
                Directory.CreateDirectory(folderPath);
            
            AssetDatabase.CreateAsset(instance, instance._assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            return instance;
        }

        private static T FindExistingInstance()
        {
            var typeName = typeof(T).Name;
            var guids = AssetDatabase.FindAssets($"t:{typeName}");

            if (guids.Length == 0)
                return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}