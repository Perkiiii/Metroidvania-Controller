using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace WorldGraphEditor.Editor
{
    internal class ValidationBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 20;

        private WorldGraphContainer _container;
        
        public void OnPreprocessBuild(BuildReport report)
        {
            if (DataValidator.IsValid(out var result, out var message)) 
                return;
            
            if (result == ValidationResult.DataMismatch)
            {
                _container = TransitionManager.LoadFromResources().Container;
                var sceneNodeData = _container.EditorData.SceneNodeData;
                var enabledScenes = EditorBuildSettings.scenes.Where(item => item.enabled).ToArray();
                    
                HandleMismatch(sceneNodeData, enabledScenes);
            }
            else
            {
                throw new BuildFailedException(message.SetColor(MessageColor.Red));
            }
        }

        private void HandleMismatch(IReadOnlyList<SceneNodeData> nodeData, EditorBuildSettingsScene[] buildScenes)
        {
            var isUserConfirmed = EditorUtility.DisplayDialog("Mismatch between Container and Build Settings",
                $"The saved scene data does not match the scenes in the Build Settings. Click \"Refresh\" to synchronize the \"{_container.name}\" container with the Build Settings." +
                $"\n\nAfter refreshing, you will need to restart the build process.",
                "Refresh", "Cancel");

            if (!isUserConfirmed)
                throw new BuildFailedException("Build process was canceled by user.".SetColor(MessageColor.Yellow));
            
            ScenesValidationHelper.AddMissingScenesToBuild(nodeData, buildScenes);
            GraphSaveUtility.RefreshPathAndBuildIndex(_container);

            var message = "The build process was stopped to apply changes. You can now retry the build.".SetColor(MessageColor.Green);
            throw new BuildFailedException(message);
        }
    }
}