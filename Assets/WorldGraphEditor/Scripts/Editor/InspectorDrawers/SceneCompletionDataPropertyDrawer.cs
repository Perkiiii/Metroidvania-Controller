using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    [CustomPropertyDrawer(typeof(SceneCompletionData))]
    internal class SceneCompletionDataPropertyDrawer : PropertyDrawer
    {
        private const string _CORRECT_ICON_NAME = "TestPassed";
        private const string _WARNING_ICON_NAME = "console.warnicon";
        private const string _ERROR_ICON_NAME = "console.erroricon";
        
        private Texture _correctIcon => EditorGUIUtility.IconContent(_CORRECT_ICON_NAME).image;
        private Texture _warningIcon => EditorGUIUtility.IconContent(_WARNING_ICON_NAME).image;
        private Texture _errorIcon => EditorGUIUtility.IconContent(_ERROR_ICON_NAME).image;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var nodeName = property.FindPropertyRelative("NodeName").stringValue;
            var duplicatesProperty = property.FindPropertyRelative("DuplicatesGuids");
            var missingProperty = property.FindPropertyRelative("Missing");
            var portsAtNodeCount = property.FindPropertyRelative("PortsAtNode").arraySize;
            var portsAtSceneCount = property.FindPropertyRelative("PortsAtSceneGuids").arraySize;

            var root = new VisualElement
            {
                style = { flexDirection = FlexDirection.Column }
            };

            var foldout = new Foldout
            {
                value = false,
                text = ""
            };
            
            var header = foldout.Q(className: "unity-base-field__input");
            
            var icon = UIToolkitUtility.CreateIcon(GetIcon(duplicatesProperty, missingProperty), 20);
            var label = UIToolkitUtility.CreateLabel($"  {nodeName} | ({portsAtSceneCount}/{portsAtNodeCount})", MessageColor.Default);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;

            header.Add(icon);
            header.Add(label);
            
            foldout.Add(new Label($"Initialized ports: {portsAtSceneCount}/{portsAtNodeCount}"));
            
            root.Add(foldout);
            
            return root;
        }

        private Texture GetIcon(SerializedProperty duplicatesProperty, SerializedProperty missingProperty)
        {
            if (duplicatesProperty.arraySize > 0)
                return _errorIcon;

            return missingProperty.arraySize > 0 ? _warningIcon : _correctIcon;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}