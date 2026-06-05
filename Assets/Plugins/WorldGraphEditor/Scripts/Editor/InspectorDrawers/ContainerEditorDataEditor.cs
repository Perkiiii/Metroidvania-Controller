using UnityEditor;

namespace WorldGraphEditor.Editor
{
    [CustomEditor(typeof(ContainerEditorData))]
    internal class ContainerEditorDataEditor : UnityEditor.Editor
    {
        private SerializedProperty _sceneNodeData;
        private SerializedProperty _edgesData;
        
        private void OnEnable()
        {
            _sceneNodeData = serializedObject.FindProperty("_sceneNodeData");
            _edgesData = serializedObject.FindProperty("_edgesData");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.Space(20);
            DrawEditorUtility.DrawDisabledFields(() =>
            {
                EditorGUILayout.PropertyField(_sceneNodeData);
                EditorGUILayout.PropertyField(_edgesData);
            });

            serializedObject.ApplyModifiedProperties();
        }
    }
}