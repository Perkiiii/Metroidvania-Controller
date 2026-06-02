using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    [CustomEditor(typeof(Object), true)]
    internal class ButtonAttributeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            var targetType = target.GetType();
            var methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var buttonAttribute = method.GetCustomAttribute<ButtonAttribute>();
                if (buttonAttribute == null) 
                    continue;
                
                var label = buttonAttribute.ButtonLabel ?? ObjectNames.NicifyVariableName(method.Name);
                if (GUILayout.Button(label))
                {
                    method.Invoke(target, null);
                }
            }
        }
    }
}