using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    [CustomPropertyDrawer(typeof(EnableIfFieldAttribute))]
    internal class EnableIfFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var target = (EnableIfFieldAttribute) attribute;
            var condition = property.serializedObject.FindProperty(target.ConditionName);

            if (condition != null)
            {
                var enabled = IsPassesCondition(condition, target.ExpectedValue);
                GUI.enabled = enabled;
                EditorGUI.PropertyField(position, property, label, true);
                GUI.enabled = true;
            }
            else
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        private static bool IsPassesCondition(SerializedProperty condition, object targetExpectedValue)
        {
            switch (condition.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    if (targetExpectedValue == null)
                        return condition.boolValue;
                    return condition.boolValue == (bool) targetExpectedValue;

                case SerializedPropertyType.Enum:
                    if (targetExpectedValue == null) 
                        return true;
                    
                    var value = (int) targetExpectedValue;
                    return condition.enumValueIndex == value;
                
                default:
                    Debug.LogError($"Unsupported property type: {condition.propertyType}");
                    return false;
            }
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}