using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal static class WGEAssetPathUtility
    {
        private const string RootFolderName = "WorldGraphEditor";
        private const string EditorAssemblyPathSuffix = "/Scripts/Editor/WGEEditor.asmdef";
        private const string LegacyRootPath = "Assets/WorldGraphEditor/";
        private const string DefaultRootPath = "Assets/Plugins/WorldGraphEditor";

        private static string _rootPath;

        internal static string RootPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_rootPath))
                    return _rootPath;

                _rootPath = FindRootPath();
                return _rootPath;
            }
        }

        internal static string GetPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return RootPath;

            var normalized = NormalizePath(relativePath);

            if (normalized.StartsWith(LegacyRootPath))
                normalized = normalized[LegacyRootPath.Length..];
            else if (normalized.StartsWith(RootPath + "/"))
                normalized = normalized[(RootPath.Length + 1)..];
            else if (normalized.StartsWith($"{RootFolderName}/"))
                normalized = normalized[(RootFolderName.Length + 1)..];

            return $"{RootPath}/{normalized}";
        }

        internal static StyleSheet LoadStyleSheet(string relativePath) => LoadAsset<StyleSheet>(relativePath);

        internal static VisualTreeAsset LoadVisualTreeAsset(string relativePath) => LoadAsset<VisualTreeAsset>(relativePath);

        internal static Texture2D LoadTexture2D(string relativePath) => LoadAsset<Texture2D>(relativePath);

        internal static Texture LoadTexture(string relativePath) => LoadAsset<Texture>(relativePath);

        internal static T LoadAsset<T>(string relativePath, bool logWarning = true) where T : Object
        {
            var path = GetPath(relativePath);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null && logWarning)
                Debug.LogWarning($"[WorldGraphEditor] Could not load {typeof(T).Name} at path: {path}");

            return asset;
        }

        internal static bool EnsureFolder(string relativePath)
        {
            var path = GetPath(relativePath);
            var parts = path.Split('/');

            if (parts.Length == 0 || parts[0] != "Assets")
            {
                Debug.LogError($"[WorldGraphEditor] Cannot create folder outside Assets: {path}");
                return false;
            }

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }

            return AssetDatabase.IsValidFolder(path);
        }

        private static string FindRootPath()
        {
            var guids = AssetDatabase.FindAssets("WGEEditor t:AssemblyDefinitionAsset");

            foreach (var guid in guids)
            {
                var path = NormalizePath(AssetDatabase.GUIDToAssetPath(guid));

                if (!path.EndsWith(EditorAssemblyPathSuffix))
                    continue;

                return path[..^EditorAssemblyPathSuffix.Length];
            }

            if (AssetDatabase.IsValidFolder(DefaultRootPath))
                return DefaultRootPath;

            Debug.LogError($"[WorldGraphEditor] Could not locate the {RootFolderName} root folder. Expected to find WGEEditor.asmdef.");
            return DefaultRootPath;
        }

        private static string NormalizePath(string path) => path.Replace('\\', '/').Trim('/');
    }
}
