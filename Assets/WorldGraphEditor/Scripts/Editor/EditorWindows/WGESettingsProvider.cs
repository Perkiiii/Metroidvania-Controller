using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal static class WGESettingsProvider
    {
        [SettingsProvider]
        internal static SettingsProvider CreateMyCustomSettingsProvider()
        {
            var provider = new SettingsProvider("Project/World Graph Editor", SettingsScope.Project)
            {
                label = "World Graph Editor",
                guiHandler = (searchContext) =>
                {
                    EditorGUIUtility.labelWidth = 260f;
                    
                    var settings = WorldGraphEditorSettings.Instance;
                    var serializedSettings = new SerializedObject(settings);
                    
                    var isToolbarExtensionsEnabledProperty = serializedSettings.FindProperty("_enableExtensions");
                    var showTransitionManagerShortcutProperty = serializedSettings.FindProperty("_showTransitionManagerShortcut");
                    var showScenesDropdown = serializedSettings.FindProperty("_showScenesDropdown");

                    var isExtensionsEnabled = isToolbarExtensionsEnabledProperty.boolValue;
                    
                    EditorGUILayout.PropertyField(serializedSettings.FindProperty("_nodeBackgroundOpacity"));
                    EditorGUILayout.PropertyField(serializedSettings.FindProperty("_hierarchyHighlighterMode"), new GUIContent("Mode"));
                    
                    EditorGUILayout.PropertyField(isToolbarExtensionsEnabledProperty);
                    EditorGUILayout.Space(6);

                    EditorGUI.BeginDisabledGroup(!isExtensionsEnabled);
                    EditorGUILayout.PropertyField(showTransitionManagerShortcutProperty);
                    EditorGUILayout.PropertyField(showScenesDropdown);
                    EditorGUI.EndDisabledGroup();

                    EditorGUILayout.PropertyField(serializedSettings.FindProperty("_customTests"));

                    ToolbarSceneExtension.Disable();
                    
                    if (isExtensionsEnabled)
                        ToolbarSceneExtension.Refresh();

                    serializedSettings.ApplyModifiedProperties();
                }
            };

            return provider;
        }
    }
}