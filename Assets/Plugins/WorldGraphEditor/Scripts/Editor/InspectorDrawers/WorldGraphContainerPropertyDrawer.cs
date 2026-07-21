using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    [CustomPropertyDrawer(typeof(WorldGraphContainer))]
    internal class WorldGraphContainerPropertyDrawer : PropertyDrawer
    {
        private const float ButtonHeight = 20f;
        private const float Spacing = 2f;

        private WorldGraphContainer _target;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            _target = property.objectReferenceValue as WorldGraphContainer;
            
            var fieldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            
            EditorGUI.PropertyField(fieldRect, property, label);
            
            if (property.objectReferenceValue == null)
                return;
            
            var buttonRect = new Rect(position.x, fieldRect.yMax + Spacing, position.width / 2 - Spacing, ButtonHeight);
            var refreshButtonRect = new Rect(buttonRect.xMax + Spacing * 2, fieldRect.yMax + Spacing, position.width / 2 - Spacing, ButtonHeight);

            if (GUI.Button(buttonRect, "Open Editor"))
            {
                OpenGraph();
            }

            var canRefresh = CanRefreshBuildSettings();
            
            EditorGUI.BeginDisabledGroup(!canRefresh);
            
            if (GUI.Button(refreshButtonRect, "Refresh Build Settings"))
            {
                RefreshBuildSettings();
            }
            
            EditorGUI.EndDisabledGroup();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = _target == null
                ? EditorGUIUtility.singleLineHeight
                : EditorGUIUtility.singleLineHeight + ButtonHeight + Spacing * 2;
            
            return height;
        }

        private void OpenGraph()
        {
            WorldBuilderGraph.OpenWindow(_target);
        }

        private void RefreshBuildSettings()
        {
            var savedScenes = _target.EditorGraph.GetScenesData();
            var enabledScenes = EditorBuildSettings.scenes.Where(item => item.enabled).ToArray();
            
            ScenesValidationHelper.AddMissingScenesToBuild(savedScenes, enabledScenes);
            GraphSaveUtility.RefreshPathAndBuildIndex(_target);
        }

        private bool CanRefreshBuildSettings()
        {
            return !ScenesValidationHelper.IsAllScenesValid(_target);
        }
    }
}