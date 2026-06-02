using UnityEditor;

namespace WorldGraphEditor.Editor
{
    [CustomEditor(typeof(WorldGraphContainer))]
    internal class WorldContainerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawEditorUtility.DrawDisabledFields(() => base.OnInspectorGUI());
        }
    }
}