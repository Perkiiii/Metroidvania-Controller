using System;
using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    [CustomPropertyDrawer(typeof(PortsDropdown))]
    internal class DropdownPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var namesData = property.FindPropertyRelative("_displayData");
            var guidData = property.FindPropertyRelative("_guidData");
            
            var selectedName = property.FindPropertyRelative("_selectedName");
            var selectedGuid = property.FindPropertyRelative("_selectedGuid");
            
            var namesArray = GetDataArray(namesData);
            var guidsArray = GetDataArray(guidData);
            
            var currentIndex = Array.IndexOf(namesArray, selectedName.stringValue);

            if (currentIndex == -1) 
                currentIndex = Array.IndexOf(guidsArray, selectedGuid.stringValue);
            
            var newIndex = EditorGUILayout.Popup(label, currentIndex, namesArray);
            
            if (newIndex >= 0 && newIndex != currentIndex)
            {
                selectedName.stringValue = namesArray[newIndex];
                selectedGuid.stringValue = guidsArray[newIndex];
            }
        }
        
        private static string[] GetDataArray(SerializedProperty property)
        {
            var arraySize = property.arraySize;
            var data = new string[arraySize];
            for (var i = 0; i < arraySize; i++)
            {
                data[i] = property.GetArrayElementAtIndex(i).stringValue;
            }

            return data;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => 0;
    }
}