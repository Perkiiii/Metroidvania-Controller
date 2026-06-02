using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WorldGraphEditor.Editor.Tests;
using FontStyle = UnityEngine.FontStyle;

namespace WorldGraphEditor.Editor
{
    [CustomPropertyDrawer(typeof(SCCGroup))]
    internal class SCCGroupPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var sccIndex = property.FindPropertyRelative("SCCIndex").intValue;
            var sccGroupProperty = property.FindPropertyRelative("TestResults");

            var root = new VisualElement();
            root.Add(new Label($"SCC Group: {sccIndex + 1}")
            {
                style =
                {
                    fontSize = 20,
                    unityFontStyleAndWeight = new StyleEnum<FontStyle> {value = FontStyle.Bold},
                    paddingBottom = 4,
                    paddingTop = 4,
                }
            });
            
            for (int i = 0; i < sccGroupProperty.arraySize; i++)
            {
                root.Add(new PropertyField(sccGroupProperty.GetArrayElementAtIndex(i)));
            }

            return root;
        }
    }
}