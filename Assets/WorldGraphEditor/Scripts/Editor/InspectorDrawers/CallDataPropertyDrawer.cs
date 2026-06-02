using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    [CustomPropertyDrawer(typeof(CallData))]
    internal class CallDataPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var callTypeProperty = property.FindPropertyRelative("EventType");
            var guidProperty = property.FindPropertyRelative("EventGuid");
            
            var fieldWidth = (position.width - 50) / 2; 
            var callTypeRect = new Rect(position.x, position.y, fieldWidth, EditorGUIUtility.singleLineHeight);
            var guidRect = new Rect(position.x + fieldWidth + 5, position.y, fieldWidth, EditorGUIUtility.singleLineHeight);
            var buttonRect = new Rect(position.x + fieldWidth * 2 + 10, position.y, 40, EditorGUIUtility.singleLineHeight);
            
            EditorGUI.PropertyField(callTypeRect, callTypeProperty, GUIContent.none);
            EditorGUI.PropertyField(guidRect, guidProperty, GUIContent.none);
            
            if (GUI.Button(buttonRect, "New")) 
                guidProperty.stringValue = System.Guid.NewGuid().ToString();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}