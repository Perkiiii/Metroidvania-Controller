using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace WorldGraphEditor.Editor
{
    internal static class SceneScreenshotUtility
    {
        private enum CaptureScope
        {
            All, 
            Available
        }
        
        internal static bool IsBatchCapturing { get; private set; }
        
        private static string ScreenshotsFolder => WGEAssetPathUtility.GetPath("Editor/Screenshots");

        public static string GetScreenshotAssetPath(string sceneGuid)
        {
            return $"{ScreenshotsFolder}/{sceneGuid}.png";
        }

        public static bool DeleteScreenshot(string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid))
                return false;

            var path = GetScreenshotAssetPath(sceneGuid);
            if (!File.Exists(Path.GetFullPath(path)))
                return false;

            WGEConsole.Log($"Screenshot deleted: {path}");

            return AssetDatabase.DeleteAsset(path);
        }

        public static long GetScreenshotFileSize(string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid))
                return -1;

            var path = GetScreenshotAssetPath(sceneGuid);
            var fullPath = Path.GetFullPath(path);

            if (!File.Exists(fullPath))
                return -1;

            return new FileInfo(fullPath).Length;
        }

        public static string FormatFileSize(long bytes)
        {
            const long KB = 1024;
            const long MB = 1024 * 1024;
            const long GB = 1024 * 1024 * 1024;

            if (bytes >= GB)
                return (bytes / (float) GB).ToString("F1") + " GB";
            if (bytes >= MB)
                return (bytes / (float) MB).ToString("F1") + " MB";
            if (bytes >= KB)
                return (bytes / (float) KB).ToString("F1") + " KB";

            return bytes + " B";
        }

        [Obsolete("possible memory leaks", true)]
        public static Sprite GetSpriteForScreenshot(string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid))
                return null;

            var path = GetScreenshotAssetPath(sceneGuid);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (texture == null)
                return null;

            var rect = new Rect(0, 0, texture.width, texture.height);
            var pivot = new Vector2(0.5f, 0.5f);
            var sprite = Sprite.Create(texture, rect, pivot);

            return sprite;
        }
        
        public static Texture2D GetScreenshotTexture(string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid))
                return null;

            return AssetDatabase.LoadAssetAtPath<Texture2D>(GetScreenshotAssetPath(sceneGuid));
        }
        
        public static Texture2D CaptureSceneScreenshot(SceneScreenshotSettings settings)
        {
            return CaptureSceneScreenshot(settings.Center, settings.Rotation, settings.Size, settings.Distance,
                settings.IsOrthographic, settings.Resolution);
        }

        public static Texture2D CaptureSceneScreenshot(Vector3 center, Quaternion rotation, Vector2 size,
            float cameraDistance, bool isOrthographic, ScreenshotResolutionType resolutionType = 0)
        {
            var camGO = new GameObject("TempCamera");
            var cam = camGO.AddComponent<Camera>();

            float[] resolutionMultipliers = {4, 16, 64};
            var multiplier = resolutionMultipliers[(int) resolutionType];

            var width = (int) (size.x * multiplier);
            var height = (int) (size.y * multiplier);

            var forward = rotation * Vector3.down;
            var cameraPos = center - forward * cameraDistance;

            cam.transform.position = cameraPos;
            cam.transform.rotation = Quaternion.LookRotation(forward, rotation * Vector3.forward);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.orthographic = isOrthographic;

            if (isOrthographic)
            {
                cam.orthographicSize = size.y / 2f;
            }
            else
            {
                var halfHeight = size.y / 2f;
                cam.fieldOfView = 2f * Mathf.Atan(halfHeight / cameraDistance) * Mathf.Rad2Deg;
            }

            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            RenderTexture.active = null;
            cam.targetTexture = null;

            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGO);

            return tex;
        }

        public static void SaveScreenshot(Texture2D texture, TextureImporterNPOTScale npotScale, string sceneAssetGuid)
        {
            var activeScene = SceneManager.GetActiveScene();

            string fileName;

            if (sceneAssetGuid != "")
            {
                fileName = $"{sceneAssetGuid}.png";
            }
            else
            {
                var scenePath = activeScene.path;
                var sceneAsset = AssetDatabase.AssetPathToGUID(scenePath);

                fileName = $"{sceneAsset}.png";
            }

            var folderPath = ScreenshotsFolder;
            var fullPath = Path.Combine(folderPath, fileName);

            WGEAssetPathUtility.EnsureFolder("Editor/Screenshots");

            var pngData = texture.EncodeToPNG();
            if (pngData != null)
            {
                File.WriteAllBytes(fullPath, pngData);
                AssetDatabase.ImportAsset(fullPath);

                var importer = (TextureImporter) AssetImporter.GetAtPath(fullPath);
                importer.textureType = TextureImporterType.GUI;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.npotScale = npotScale;

                importer.SaveAndReimport();

                WGEConsole.Log($"Screenshot saved: {fullPath}");
            }
            else
            {
                WGEConsole.Warning("Failed to encode texture to PNG.");
            }
        }
        
        public static void CaptureAllScenes(WorldGraphContainer container)
        {
            CaptureScenes(GetCaptureTargets(container, includeDisabled: true), CaptureScope.All);
        }

        public static void CaptureAvailableScenes(WorldGraphContainer container)
        {
            var sceneNodes = GetCaptureTargets(container, includeDisabled: false);
            if (sceneNodes.Count == 0)
            {
                WGEConsole.Warning("No scenes available for auto-capture.");
                return;
            }
            CaptureScenes(sceneNodes, CaptureScope.Available);
        }

        private static IReadOnlyList<SceneNodeData> GetCaptureTargets(WorldGraphContainer container, bool includeDisabled)
        {
            return  container.EditorGraph.GetScenesData()
                .Where(n => n.SceneAsset != null)
                .Where(n => includeDisabled || SceneScreenshotsData.Instance.IsAutoCaptureEnabled(ResolveSceneGuid(n)))
                //DistinctBy
                .GroupBy(ResolveSceneGuid)
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .Select(g => g.First())
                //
                .ToArray();
        }

        private static string ResolveSceneGuid(SceneNodeData node)
        {
            return string.IsNullOrEmpty(node.SceneAssetGuid)
                ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(node.SceneAsset))
                : node.SceneAssetGuid;
        }

        private static void CaptureScenes(IReadOnlyList<SceneNodeData> sceneNodes, CaptureScope scope)
        {
            if (!EditorUtility.DisplayDialog(
                    "Capture Scene Previews",
                    $"{scope} scenes will be loaded one by one to capture their previews.\n\nThis may take some time. Continue?",
                    "Continue",
                    "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var originalScenePath = SceneManager.GetActiveScene().path;
            var cancelRequested = false;
            
            IsBatchCapturing = true;
            
            try
            {
                for (int index = 0; index < sceneNodes.Count; index++)
                {
                    var nodeData = sceneNodes[index];

                    if (EditorUtility.DisplayCancelableProgressBar(
                            $"Capture Scene Previews ({index + 1}/{sceneNodes.Count})",
                            $"Processing: {nodeData.SceneAsset.name}",
                            (float) (index + 1) / sceneNodes.Count))
                    {
                        cancelRequested = true;
                        break;
                    }

                    var scenePath = AssetDatabase.GetAssetPath(nodeData.SceneAsset);
                    var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);

                    if (string.IsNullOrEmpty(scenePath))
                        continue;

                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                    var settings = SceneScreenshotsData.Instance.GetScreenshotData(sceneGuid, out _);
                    var texture = CaptureSceneScreenshot(settings);

                    try
                    {
                        SaveScreenshot(texture, settings.NpotScale, sceneGuid);
                    }
                    finally
                    {
                        Object.DestroyImmediate(texture);
                    }
                }
            }
            finally
            {
                IsBatchCapturing = false;
                
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                EditorUtility.ClearProgressBar();
                
                if (cancelRequested)
                    WGEConsole.Warning("Capture cancelled by user.");
            }
        }
    }
}