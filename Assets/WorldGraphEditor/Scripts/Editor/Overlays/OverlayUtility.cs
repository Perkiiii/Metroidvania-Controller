using UnityEngine.SceneManagement;

namespace WorldGraphEditor.Editor.Overlays
{
    internal static class OverlayUtility
    {
        public static string GetContainerStatus(out bool isDataValid)
        {
            isDataValid = true;
            
            if (!DataValidator.IsValid(out var result, out _))
            {
                isDataValid = false;
                
                switch (result)
                {
                    case ValidationResult.ManagerIsNull:
                        return "Transition Manager does not exist";
                    case ValidationResult.ContainerIsNull:
                        return "Container is not assigned";
                    case ValidationResult.ContainerHasErrors:
                        return "Container contains errors";
                    case ValidationResult.NoData:
                        return "Container has no data";
                    case ValidationResult.DataMismatch:
                        return "Scene data does not match Build Settings";
                }
            }
            
            isDataValid = true;
            var containerName = TransitionManager.LoadFromResources().Container.name;
            return $"Used container: \"{containerName}\"";
        }

        public static string GetSceneStatus(out bool isSceneValid)
        {
            isSceneValid = true;
            
            var isDataValid = DataValidator.IsValid(out _, out _);
            
            if (!isDataValid)
                return "";

            isSceneValid = false;
            
            var container = TransitionManager.LoadFromResources().Container;
            var sceneData = container.EditorData.GetSceneDataByPath(SceneManager.GetActiveScene().path, out var isDataExists);
            
            if (!isDataExists)
                return "Container has no data for scene".SetColor(MessageColor.Yellow);

            if (sceneData.PortsData == null || sceneData.PortsData.Length == 0)
                return "No ports on scene".SetColor(MessageColor.Yellow);

            isSceneValid = true;
            return "";
        }
    }
}