using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal class SceneScreenshotsData : ScriptableSingleton<SceneScreenshotsData>
    {
        [SerializeField] private List<SceneScreenshotSettings> _screenshotSettings = new();
        [SerializeField] private SceneScreenshotSettings _defaultSettings = SceneScreenshotSettings.Default;

        public void SaveScreenshotSettings(SceneScreenshotSettings settings)
        {
            var index = _screenshotSettings.FindIndex(item => item.SceneGuid == settings.SceneGuid);

            if (index != -1)
            {
                _screenshotSettings[index] = settings;
            }
            else
            {
                _screenshotSettings.Add(settings);
            }

            RegisterChanges();
        }

        public SceneScreenshotSettings GetScreenshotData(string sceneGuid, out bool hasCustomData)
        {
            hasCustomData = false;
            
            var index = _screenshotSettings.FindIndex(item => item.SceneGuid == sceneGuid);
            SceneScreenshotSettings data;
            
            if (index == -1)
                data = _defaultSettings;
            else
            {
                data = _screenshotSettings[index];
                hasCustomData = true;
            }

            return data;
        }

        public void SaveAsDefaultScreenshotSettings(SceneScreenshotSettings settings)
        {
            _defaultSettings = settings;
            RegisterChanges();
        }

        public void RemoveScreenshotSettings(SceneScreenshotSettings settings)
        {
            var index = _screenshotSettings.FindIndex(item => item.SceneGuid == settings.SceneGuid);
            
            if (index != -1)
                _screenshotSettings.RemoveAt(index);
            
            RegisterChanges();
        }
        
        private void RegisterChanges()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
            AssetDatabase.Refresh();
        }
    }
}