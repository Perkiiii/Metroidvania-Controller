using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WorldGraphEditor.Editor
{
    [CustomPropertyDrawer(typeof(RequireInterfaceAttribute))]
    internal class RequireInterfaceDrawer : PropertyDrawer
    {
        private RequireInterfaceAttribute _requireInterfaceAttribute => (RequireInterfaceAttribute) attribute;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var requiredInterfaceType = _requireInterfaceAttribute.InterfaceType;

            EditorGUI.BeginProperty(position, label, property);

            if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
            {
                DrawArrayField(position, property, label, requiredInterfaceType);   
            }
            else
            {
                DrawInterfaceObjectField(position, property, label, requiredInterfaceType);
            }
            
            EditorGUI.EndProperty();

            var args = new InterfaceArgs(GetTypeOrElementType(fieldInfo.FieldType), requiredInterfaceType);
            InterfaceReferenceUtility.OnGUI(position, property, label, args);
        }

        private void DrawArrayField(Rect position, SerializedProperty property, GUIContent label, Type interfaceType)
        {
            property.arraySize =
                EditorGUI.IntField(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                    label.text + "Size", property.arraySize);

            var yOffset = EditorGUIUtility.singleLineHeight;
            for (int i = 0; i < property.arraySize; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                var elementRect = new Rect(position.x, position.y + yOffset, position.width,
                    EditorGUIUtility.singleLineHeight);
                DrawInterfaceObjectField(elementRect, element, new GUIContent($"Element {i}"), interfaceType);
                yOffset += EditorGUIUtility.singleLineHeight;
            }
        }

        private void DrawInterfaceObjectField(Rect position, SerializedProperty property, GUIContent label, Type interfaceType)
        {
            var oldRef = property.objectReferenceValue;
            var newRef = EditorGUI.ObjectField(position, label, oldRef, typeof(Object), true);

            if (newRef != null && newRef != oldRef)
            {
                ValidateAndAssignObject(property, newRef, interfaceType);
            }
            else if (newRef == null)
            {
                property.objectReferenceValue = null;
            }
        }

        private void ValidateAndAssignObject(SerializedProperty property, Object newRef, Type interfaceType)
        {
            if (newRef is GameObject gameObject)
            {
                var component = gameObject.GetComponent(interfaceType);

                if (component != null)
                {
                    property.objectReferenceValue = component;
                    return;
                }
            } else if (interfaceType.IsAssignableFrom(newRef.GetType()))
            {
                property.objectReferenceValue = newRef;
                return;
            }
            
            Debug.LogWarning($"The assigned object does not implement '{interfaceType.Name}'.");
            property.objectReferenceValue = null;
        }

        private Type GetTypeOrElementType(Type type)
        {
            if (type.IsArray) 
                return type.GetElementType();
            
            if (type.IsGenericType) 
                return type.GetGenericArguments()[0];
            
            return type;
        }
    }
}