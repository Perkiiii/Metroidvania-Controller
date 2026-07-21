using System;
using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal static class DrawEditorUtility
    {
        public static void DrawPropertyField(this SerializedProperty serializedProperty)
        {
            EditorGUILayout.PropertyField(serializedProperty);
        }
        
        public static void DrawArray(SerializedProperty serializedProperty)
        {   
            for (int i = 0; i < serializedProperty.arraySize; i++)
            {
                serializedProperty.GetArrayElementAtIndex(i).DrawPropertyField();
            }
        }
        
        public static void DrawDisabledFields(Action action, bool drawDisabled = true)
        {
            EditorGUI.BeginDisabledGroup(drawDisabled);
            action.Invoke();
            EditorGUI.EndDisabledGroup();
        }
        
        public static void DrawHorizontalFields(Action action)
        {
            EditorGUILayout.BeginHorizontal();
            action.Invoke();
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawHeader(string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }
        
        public static int DrawPopup(string label, int selectedIndex, string[] options)
        {
            return EditorGUILayout.Popup(label, selectedIndex, options);
        }
        
        public static void DrawMessage(string message, MessageType messageType = MessageType.Warning)
        {
            EditorGUILayout.HelpBox(message, messageType);
        }

        public static void DrawSpace(int amount = 12)
        {
            EditorGUILayout.Space(amount);
        }

        public static bool DrawButton(string text, bool enabled, GUIStyle style = null)
        {
            var previous = GUI.enabled;
            GUI.enabled = enabled;

            style ??= GUI.skin.button;  
            
            var res = GUILayout.Button(text, style);
            GUI.enabled = previous;

            return res;
        }
    }
}