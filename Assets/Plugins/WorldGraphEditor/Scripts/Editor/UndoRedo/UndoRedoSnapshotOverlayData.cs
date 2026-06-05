using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal class UndoRedoSnapshotOverlayData : ScriptableSingleton<UndoRedoSnapshotOverlayData>
    {
        [SerializeField, ReadOnlyField] public SceneScreenshotSettings ScreenshotSettings;
        
        public void RegisterChanges(SceneScreenshotSettings getScreenshotSettings)
        {
            Undo.RecordObject(Instance, "Scene Screenshot Settings Changed");
            ScreenshotSettings = getScreenshotSettings;
            EditorUtility.SetDirty(Instance);
        }
    }
}