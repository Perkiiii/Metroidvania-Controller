using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    [CustomPropertyDrawer(typeof(SceneTestResult))]
    internal class SceneTestResultPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var nodeName = property.FindPropertyRelative("NodeName").stringValue;
            var errorsCount = property.FindPropertyRelative("ErrorsCount").intValue;
            var warningsCount = property.FindPropertyRelative("WarningsCount").intValue;
            var tested = property.FindPropertyRelative("Tested").boolValue;
            var testResultsProperty = property.FindPropertyRelative("TestResults");
            var scenePath = property.FindPropertyRelative("ScenePath").stringValue;
            
            var root = new VisualElement {style = {flexDirection = FlexDirection.Column}};

            var icon = UIToolkitUtility.CreateIcon(GetIcon(errorsCount, warningsCount, tested), 16);
            var label = UIToolkitUtility.CreateLabel($"  {nodeName}", MessageColor.Default);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;

            var foldout = new FoldoutWithIcon(icon, label);
            
            for (int i = 0; i < testResultsProperty.arraySize; i++)
            {
                var item = testResultsProperty.GetArrayElementAtIndex(i);
                var testStatusType = item.FindPropertyRelative("TestStatusType").enumValueIndex;
                var testPassed = testStatusType == 0;
                var color = testPassed ? MessageColor.Green.GetColor() : GetColor(testStatusType);

                var message = item.FindPropertyRelative("Description").stringValue;
                
                var textItem = new ColoredText(message, color);
                foldout.Add(textItem);
            }

            var buttons = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignSelf = Align.FlexEnd,
                }
            };
            
            buttons.Add(UIToolkitUtility.GetLoadSceneButton(scenePath, "Open Scene"));
            
            foldout.Add(buttons);
            root.Add(foldout);

            return root;
        }

        private static Color GetColor(int intValue)
        {
            if (intValue == 0)
                return MessageColor.Green.GetColor();

            return intValue == 1 ? MessageColor.Yellow.GetColor() : MessageColor.Red.GetColor();
        }

        private static Texture GetIcon(int errorsCount, int warningsCount, bool tested)
        {
            if (!tested)
                return EditorGUIUtility.IconContent(UIToolkitUtility.INFO_ICON_NAME).image;
            
            if (errorsCount > 0)
                return UIToolkitUtility.ERROR_ICON_WGE;

            return warningsCount > 0 ? UIToolkitUtility.WARNING_ICON_WGE : UIToolkitUtility.CORRECT_ICON_WGE;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}