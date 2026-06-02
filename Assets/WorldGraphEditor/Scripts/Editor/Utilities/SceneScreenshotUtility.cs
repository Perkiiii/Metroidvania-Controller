using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace WorldGraphEditor.Editor
{
    internal static class SceneScreenshotUtility
    {
        public static long GetScreenshotFileSize(string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid))
                return -1;

            var path = $"Assets/WorldGraphEditor/Editor/Screenshots/{sceneGuid}.png";
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
                return (bytes / (float)GB).ToString("F1") + " GB";
            if (bytes >= MB)
                return (bytes / (float)MB).ToString("F1") + " MB";
            if (bytes >= KB)
                return (bytes / (float)KB).ToString("F1") + " KB";

            return bytes + " B";
        }
        
        public static Sprite GetSpriteForScreenshot(string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid))
                return null;

            var path = $"Assets/WorldGraphEditor/Editor/Screenshots/{sceneGuid}.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (texture == null)
                return null;
            
            var rect = new Rect(0, 0, texture.width, texture.height);
            var pivot = new Vector2(0.5f, 0.5f);
            var sprite = Sprite.Create(texture, rect, pivot);
            
            return sprite;
        }

        public static Texture2D CaptureSceneScreenshot(SceneScreenshotSettings settings) =>
            CaptureSceneScreenshot(settings.Center, settings.Rotation, settings.Size, settings.Distance,
                settings.IsOrthographic, settings.Resolution);
        
        public static Texture2D CaptureSceneScreenshot(Vector3 center, Quaternion rotation, Vector2 size, float cameraDistance, bool isOrthographic, ScreenshotResolutionType resolutionType = 0)
        {
            var camGO = new GameObject("TempCamera");
            var cam = camGO.AddComponent<Camera>();
            
            float[] resolutionMultipliers = { 4, 16, 64}; 
            var multiplier = resolutionMultipliers[(int)resolutionType];
            
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
            
            var folderPath = "Assets/WorldGraphEditor/Editor/Screenshots/";
            var fullPath = Path.Combine(folderPath, fileName);
            
            if (!AssetDatabase.IsValidFolder("Assets/WorldGraphEditor/Editor"))
                AssetDatabase.CreateFolder("Assets/WorldGraphEditor", "Editor");

            if (!AssetDatabase.IsValidFolder($"Assets/WorldGraphEditor/Editor/Screenshots"))
                AssetDatabase.CreateFolder("Assets/WorldGraphEditor/Editor", "Screenshots");
            
            var pngData = texture.EncodeToPNG();
            if (pngData != null)
            {
                File.WriteAllBytes(fullPath, pngData);
                AssetDatabase.ImportAsset(fullPath);
                
                var importer = (TextureImporter)AssetImporter.GetAtPath(fullPath);
                importer.textureType = TextureImporterType.GUI;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.npotScale = npotScale;
                
                importer.SaveAndReimport();
                
                Debug.Log($"Screenshot saved: {fullPath}");
            }
            else
            {
                Debug.LogWarning("Failed to encode texture to PNG.");
            }
        }

        public static void CaptureAllScenes(WorldGraphContainer container)
        {
            if (!EditorUtility.DisplayDialog(
                    "Capture Scene Previews",
                    "All scenes will be loaded one by one to capture their previews.\n\nThis may take some time. Continue?",
                    "Continue",
                    "Cancel"))
                return;
            
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) 
                return;
            
            var originalScenePath = SceneManager.GetActiveScene().path;
            var sceneNodes = container.EditorData.SceneNodeData;
            var cancelRequested = false;
            
            try
            {
                for (int i = 0; i < sceneNodes.Count; i++)
                {
                    var nodeData = sceneNodes[i];

                    var scenePath = AssetDatabase.GetAssetPath(nodeData.SceneAsset);
                    var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);

                    if (string.IsNullOrEmpty(scenePath))
                        continue;

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Capture Scene Previews",
                            $"Processing: {nodeData.SceneAsset.name}",
                            (float) i / sceneNodes.Count))
                    {
                        cancelRequested = true;
                        break;
                    }

                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                    var settings = SceneScreenshotsData.Instance.GetScreenshotData(sceneGuid, out _);
                    var texture = CaptureSceneScreenshot(settings);
                    
                    SaveScreenshot(texture, settings.NpotScale, sceneGuid);
                }
            }
            finally
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                EditorUtility.ClearProgressBar();

                if (cancelRequested)
                    WGEConsole.Warning("Capture cancelled by user.");
                else
                    WGEConsole.Log("Capture finished.");
            }
        }
    }
}