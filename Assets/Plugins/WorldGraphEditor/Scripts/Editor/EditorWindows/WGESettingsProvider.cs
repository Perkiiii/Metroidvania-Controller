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

                    EditorGUILayout.Space(12);
                    DrawGraphEditorSection();
                    EditorGUILayout.Space(6);

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    DrawProjectSettingsSection();
                    EditorGUILayout.EndVertical();
                }
            };

            return provider;
        }

        private static void DrawGraphEditorSection()
        {
            var settings = WorldGraphEditorSettings.Instance;
            var serializedSettings = new SerializedObject(settings);

            EnsureProjectConfigLinked(serializedSettings);
            var container = settings.ProjectConfig.Container;

            EditorGUI.BeginChangeCheck();
            DrawGraphEditorCoreGroup(serializedSettings, out var isExtensionsEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                serializedSettings.ApplyModifiedProperties();
                WGEEditorEvents.Notify(WGEEditorChangeType.GraphEditor | WGEEditorChangeType.GraphSave);
            }

            EditorGUILayout.Space(6);

            EditorGUI.BeginChangeCheck();
            DrawCustomTestsAndValidationGroup(serializedSettings, container);
            if (EditorGUI.EndChangeCheck())
            {
                serializedSettings.ApplyModifiedProperties();
                WGEEditorEvents.Notify(WGEEditorChangeType.Validation);
            }

#if !UNITY_6000_3_OR_NEWER
            ToolbarSceneExtension.Disable();

            if (isExtensionsEnabled)
                ToolbarSceneExtension.Refresh();
#endif
        }

        private static void DrawGraphEditorCoreGroup(SerializedObject serializedSettings, out bool isExtensionsEnabled)
        {
            var isToolbarExtensionsEnabledProperty = serializedSettings.FindProperty("_enableExtensions");
            var showTransitionManagerShortcutProperty = serializedSettings.FindProperty("_showTransitionManagerShortcut");
            var showScenesDropdown = serializedSettings.FindProperty("_showScenesDropdown");
            isExtensionsEnabled = isToolbarExtensionsEnabledProperty.boolValue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Graph Editor", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_nodeBackgroundOpacity"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_hierarchyHighlighterMode"), new GUIContent("Mode"));
            EditorGUILayout.PropertyField(
                serializedSettings.FindProperty("_forceRefreshAffectedScenesOnSave"),
                new GUIContent("Force-refresh affected scenes on graph save"));

            EditorGUILayout.PropertyField(isToolbarExtensionsEnabledProperty);
            EditorGUILayout.Space(6);

            EditorGUI.BeginDisabledGroup(!isExtensionsEnabled);
            EditorGUILayout.PropertyField(showTransitionManagerShortcutProperty);
            EditorGUILayout.PropertyField(showScenesDropdown);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        private static void DrawCustomTestsAndValidationGroup(SerializedObject serializedSettings, WorldGraphContainer container)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.PropertyField(serializedSettings.FindProperty("_customTests"));

            EditorGUILayout.HelpBox(
                "Validate runs scene tests on the active container and opens the Project Validation Result asset.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            
            if (DrawEditorUtility.DrawButton("Validate Project",ProjectValidationUtility.CanValidate(container)))
                ProjectValidationUtility.RunValidationAndPingResult(container);

            if (DrawEditorUtility.DrawButton("Open Result", ProjectValidationResult.HasInstance))
            {
                var result = ProjectValidationResult.Instance;
                EditorGUIUtility.PingObject(result);
                Selection.activeObject = result;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static void DrawProjectSettingsSection()
        {
            var serializedSettings = new SerializedObject(WorldGraphEditorSettings.Instance);
            var resolver = EnsureProjectConfigLinked(serializedSettings).objectReferenceValue as WGEProjectConfig
                           ?? WGEProjectConfig.FindOrCreateInstance();

            var serializedResolver = new SerializedObject(resolver);

            EditorGUI.BeginChangeCheck();
            DrawProjectConfigGroup(serializedResolver);
            if (EditorGUI.EndChangeCheck())
            {
                serializedResolver.ApplyModifiedProperties();
                WGEProjectConfig.ClearInstanceCache();
                WGEEditorEvents.Notify(WGEEditorChangeType.ProjectConfig);
            }

            EditorGUILayout.Space(6);
        }

        private static void DrawProjectConfigGroup(SerializedObject serializedResolver)
        {
            var useCustomTransitionManager = serializedResolver.FindProperty("_useCustomTransitionManager");
            var isCustomTransitionManagerEnabled = useCustomTransitionManager.boolValue;

            EditorGUILayout.LabelField("Project Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(useCustomTransitionManager);

            EditorGUI.BeginDisabledGroup(!isCustomTransitionManagerEnabled);
            EditorGUILayout.PropertyField(serializedResolver.FindProperty("_container"));
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.BeginDisabledGroup(isCustomTransitionManagerEnabled);
            DrawProjectSettingsActions();
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawProjectSettingsActions()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Open Transition Manager"))
                TransitionManagerPrefabCreator.CreateOrOpenPrefab();

            EditorGUILayout.EndHorizontal();
        }
        

        private static SerializedProperty EnsureProjectConfigLinked(SerializedObject serializedSettings)
        {
            var projectConfigProperty = serializedSettings.FindProperty("_projectConfig");

            if (projectConfigProperty.objectReferenceValue == null)
            {
                projectConfigProperty.objectReferenceValue = WGEProjectConfig.FindOrCreateInstance();
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            }

            return projectConfigProperty;
        }
    }
}
