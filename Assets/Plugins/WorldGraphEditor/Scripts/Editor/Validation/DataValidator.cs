namespace WorldGraphEditor.Editor
{
    internal static class DataValidator
    {
        public static bool IsValid(out ValidationResult result, out string message)
        {
            var manager = TransitionManager.LoadFromResources();
            var isContainerHasErrors = true;
            WorldGraphContainer container = null;
            result = ValidationResult.Ok;
            message = "";

            if (manager != null)
                container = manager.Container;

            if (container != null)
                isContainerHasErrors = container.ContainsErrors();

            if (ScenesValidationHelper.IsAllScenesValid(container) && !isContainerHasErrors)
                return true;

            if (manager == null)
            {
                result = ValidationResult.ManagerIsNull;
                message =
                    "The TransitionManager instance is missing. To fix this, create a new TransitionManager " +
                    "via the context menu: \"Tools/World Graph Editor/Create Transition Manager Prefab\".";
            }
            else if (container == null)
            {
                result = ValidationResult.ContainerIsNull;
                message = "\"The \"Container\" field in TransitionManager is not assigned. Please specify a valid World Graph Container.";
            }
            else if (!container.HasData)
            {
                result = ValidationResult.NoData;
                message = "The \"Container\" has no stored nodes or edges.";
            }
            else if (container.ContainsErrors())
            {
                result = ValidationResult.ContainerHasErrors;
                message =
                    "The \"Container\" contains validation errors. " +
                    "In the Word Graph Editor window, select the node highlighted in red to get more detailed information. " +
                    "Please review the container's data and fix the detected errors.";
            }
            else
            {
                result = ValidationResult.DataMismatch;
                message =
                    "The saved scene data does not match the scenes in the Build Settings. " +
                    "Please click the \"Refresh Build Settings\" button in the \"Container\" " +
                    "field in TransitionManager to resolve this issue.";
            }

            return false;
        }
    }
    
    public enum ValidationResult
    {
        Ok,
        ManagerIsNull,
        ContainerIsNull,
        ContainerHasErrors,
        DataMismatch,
        NoData
    }
}