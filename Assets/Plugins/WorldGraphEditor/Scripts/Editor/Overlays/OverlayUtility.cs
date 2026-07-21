using UnityEngine.SceneManagement;

namespace WorldGraphEditor.Editor.Overlays
{
    internal static class OverlayUtility
    {
        public static string GetContainerStatus(out bool isDataValid)
        {
            isDataValid = true;
            
            if (!DataValidator.IsValid(out var result))
            {
                isDataValid = false;
                
                switch (result)
                {
                    case ValidationResult.ManagerIsNull:
                        return "Transition Manager does not exist";
                    case ValidationResult.DefaultSettingsContainerIsNull:
                        return "Container is not assigned in Transition Manager";
                    case ValidationResult.CustomSettingsContainerIsNull:
                        return "Container is not assigned in Project Settings";
                    case ValidationResult.ContainerHasErrors:
                        return "Container has errors";
                    case ValidationResult.NoData:
                        return "Container has no data";
                    case ValidationResult.BuildSettingsMismatch:
                        return "Scene data does not match Build Settings";
                }
            }
            
            isDataValid = true;
            var containerName = WGEProjectConfig.Instance.Container.name;
            return $"Container: \"{containerName}\"";
        }

        public static string GetSceneStatus(out bool isSceneValid)
        {
            isSceneValid = true;
            
            var isDataValid = DataValidator.IsValid(out _);
            
            if (!isDataValid)
                return "";

            isSceneValid = false;
            
            var editorGraph = WGEProjectConfig.Instance.GetEditorGraph();
            var isDataExists = editorGraph.TryGetSceneDataByPath(SceneManager.GetActiveScene().path, out var sceneData);
            
            if (!isDataExists)
                return "Container has no data for scene".SetColor(MessageColor.Yellow);

            if (sceneData.PortsData == null || sceneData.PortsData.Length == 0)
                return "No ports on scene".SetColor(MessageColor.Yellow);

            isSceneValid = true;
            return "";
        }

        public static string GetManagerStatus()
        {
            return WGEProjectConfig.Instance.IsCustomManagerEnabled ? "Custom".SetColor(MessageColor.Green) : "Default";
        }
    }
}