using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WorldGraphEditor.Editor
{
    [CustomPropertyDrawer(typeof(InterfaceReference<>))]
    [CustomPropertyDrawer(typeof(InterfaceReference<,>))]
    internal class InterfaceReferencePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var valueProperty = property.FindPropertyRelative("_value");
            var args = GetArguments(fieldInfo);

            EditorGUI.BeginProperty(position, label, property);

            var assignedObject = EditorGUI.ObjectField(position, label, valueProperty.objectReferenceValue, typeof(Object), true);

            if (assignedObject != null)
            {
                if (assignedObject is GameObject gameObject)
                {
                    ValidateAndAssignObjects(valueProperty, gameObject.GetComponent(args.InterfaceType), gameObject.name, args.InterfaceType.Name);
                }
                else
                {
                    ValidateAndAssignObjects(valueProperty, assignedObject, args.InterfaceType.Name);
                }
            }
            else
            {
                valueProperty.objectReferenceValue = null;
            }
            
            EditorGUI.EndProperty();
            
            InterfaceReferenceUtility.OnGUI(position, valueProperty, label, args);
        }

        private static InterfaceArgs GetArguments(FieldInfo fieldInfo)
        {
            var fieldType = fieldInfo.FieldType;

            if (!TryGetTypesFromInterfaceReference(fieldType, out var objectType, out var interfaceType))
            {
                GetTypesFromList(fieldType, out objectType, out interfaceType);
            }

            return new InterfaceArgs(objectType, interfaceType);

            void GetTypesFromList(Type type, out Type objType, out Type intType)
            {
                objType = null;
                intType = null;

                var interfaces = type.GetInterfaces().FirstOrDefault(item => item.IsGenericType && item.GetGenericTypeDefinition() == typeof(IList<>));

                if (interfaces == null) 
                    return;
                
                var elementType = interfaces.GetGenericArguments()[0];
                TryGetTypesFromInterfaceReference(elementType, out objType, out intType);
            }

            bool TryGetTypesFromInterfaceReference(Type type, out Type objType, out Type intType)
            {
                objType = null;
                intType = null;

                if (type?.IsGenericType != true)
                    return false;

                var genericType = type.GetGenericTypeDefinition();

                if (genericType == typeof(InterfaceReference<>))
                    type = type.BaseType;

                if (type?.GetGenericTypeDefinition() != typeof(InterfaceReference<,>)) 
                    return false;
                
                var types = type.GetGenericArguments();
                intType = types[0];
                objType = types[1];
                return true;
            }
        }

        private static void ValidateAndAssignObjects(SerializedProperty property, Object targetObject, string componentName, string interfaceName = null)
        {
            if (targetObject != null)
            {
                property.objectReferenceValue = targetObject;
            }
            else
            {
                var msg = interfaceName != null
                    ? $"GameObject '{componentName}'"
                    : "assigned object";
                
                Debug.LogWarning($"The {msg} does not have a component that implements '{interfaceName}.'");

                property.objectReferenceValue = null;
            }
        }
    }

    public struct InterfaceArgs
    {
        public readonly Type ObjectType;
        public readonly Type InterfaceType;

        public InterfaceArgs(Type objectType, Type interfaceType)
        {
            Debug.Assert(typeof(Object).IsAssignableFrom(objectType), $"{nameof(objectType)} needs to be of Type {typeof(Object)}.");
            Debug.Assert(interfaceType.IsInterface, $"{nameof(interfaceType)} needs to be an interface.");

            ObjectType = objectType;
            InterfaceType = interfaceType;
        }
    }
}