using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal class SceneScreenshotsData : ScriptableSingleton<SceneScreenshotsData>
    {
        [SerializeField] private List<SceneScreenshotSettings> _screenshotSettings = new();
        [SerializeField] private SceneScreenshotSettings _defaultSettings = SceneScreenshotSettings.Default;
        [SerializeField] private List<string> _autoCaptureDisabledSceneGuids = new();

        public static event Action AutoCaptureSettingsChanged;
        public static event Action ScreenshotAboutToCapture;
        public static event Action ScreenshotCaptured;

        internal static void RaiseScreenshotAboutToCapture() => ScreenshotAboutToCapture?.Invoke();
        internal static void RaiseScreenshotCaptured() => ScreenshotCaptured?.Invoke();

        public bool IsAutoCaptureEnabled(string sceneGuid) =>
            !string.IsNullOrEmpty(sceneGuid) && !_autoCaptureDisabledSceneGuids.Contains(sceneGuid);

        public void SetAutoCaptureEnabled(string sceneGuid, bool enabled)
        {
            if (string.IsNullOrEmpty(sceneGuid))
                return;

            var index = _autoCaptureDisabledSceneGuids.IndexOf(sceneGuid);
            var isCurrentlyEnabled = index == -1;

            if (enabled == isCurrentlyEnabled)
                return;

            Undo.RecordObject(this, enabled ? "Enable Preview-Capture" : "Disable Preview-Capture");

            if (enabled)
                _autoCaptureDisabledSceneGuids.RemoveAt(index);
            else
            {
                _autoCaptureDisabledSceneGuids.Add(sceneGuid);
            }

            RegisterChanges();
            AutoCaptureSettingsChanged?.Invoke();
        }

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