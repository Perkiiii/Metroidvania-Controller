using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    [CustomPropertyDrawer(typeof(ReadOnlyFieldAttribute))]
    internal class ReadOnlyFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            DrawEditorUtility.DrawDisabledFields(() => EditorGUI.PropertyField(position, property, label, true));
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}