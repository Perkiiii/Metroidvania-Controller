using System;
using System.IO;
using UnityEngine;

namespace WorldGraphEditor
{
    public class WGEProjectConfig : ScriptableObject
    {
        private const string _ASSET_FOLDER = "Assets/Plugins/WorldGraphEditor/Resources/Settings";
        private const string _ASSET_FILE_NAME = "WGEProjectConfig.asset";
        private static readonly string _RESOURCE_PATH = Path.Combine("Settings", "WGEProjectConfig");

        [SerializeField] private bool _useCustomTransitionManager = false;
        [SerializeField] private WorldGraphContainer _container;

        private static WGEProjectConfig _cachedInstance;
        private static WGEProjectConfig LoadFromResources()
        {
            return Resources.Load<WGEProjectConfig>(_RESOURCE_PATH);
        }

#if !UNITY_EDITOR
        public static WGEProjectConfig? Instance => _cachedInstance ??= LoadFromResources();
#endif

        public WorldGraphContainer Container => _useCustomTransitionManager ? _container : TransitionManager.LoadFromResources()?.Container;

        public bool IsCustomManagerEnabled => _useCustomTransitionManager;

        public WorldGraph GetWorldGraph()
        {
            if (Container == null)
                throw new InvalidOperationException($"{nameof(WGEProjectConfig)}: {nameof(Container)} is not assigned.");

            return Container.GetWorldGraph();
        }

#if UNITY_EDITOR
        public static WGEProjectConfig Instance => _cachedInstance ??= FindOrCreateInstance();

        public static void ClearInstanceCache() => _cachedInstance = null;

        public EditorGraph GetEditorGraph() => Container?.EditorGraph;

        public static WGEProjectConfig FindOrCreateInstance()
        {
            ValidateSingleInstance(out var guids);

            if (guids.Length > 0)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                return UnityEditor.AssetDatabase.LoadAssetAtPath<WGEProjectConfig>(path);
            }

            return CreateInstanceAsset();
        }

        public static void ValidateSingleInstance()
        {
            ValidateSingleInstance(out _);
        }

        private static void ValidateSingleInstance(out string[] guids)
        {
            guids = UnityEditor.AssetDatabase.FindAssets($"t:{nameof(WGEProjectConfig)}");

            if (guids.Length <= 1)
                return;

            foreach (var guid in guids)
            {
                WGEConsole.Error(
                    $"Duplicate {nameof(WGEProjectConfig)} at {UnityEditor.AssetDatabase.GUIDToAssetPath(guid)}");
            }
        }

        private static WGEProjectConfig CreateInstanceAsset()
        {
            EnsureAssetFolderExists();

            var instance = CreateInstance<WGEProjectConfig>();
            var path = Path.Combine(_ASSET_FOLDER, _ASSET_FILE_NAME);
            UnityEditor.AssetDatabase.CreateAsset(instance, path);
            UnityEditor.AssetDatabase.SaveAssets();
            WGEConsole.Log($"Created {nameof(WGEProjectConfig)} at {path}");

            return instance;
        }

        private static void EnsureAssetFolderExists()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Plugins/WorldGraphEditor/Resources"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Plugins/WorldGraphEditor", "Resources");

            if (!UnityEditor.AssetDatabase.IsValidFolder(_ASSET_FOLDER))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Plugins/WorldGraphEditor/Resources", "Settings");
        }

        private void OnValidate()
        {
            ValidateSingleInstance();
        }
#endif
    }
}
