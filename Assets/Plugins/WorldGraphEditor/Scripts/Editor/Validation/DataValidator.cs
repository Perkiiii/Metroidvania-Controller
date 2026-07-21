using System;

namespace WorldGraphEditor.Editor
{
    internal static class DataValidator
    {
        internal static bool IsValid(out ValidationResult result)
        {
            var wgeProjectConfig = WGEProjectConfig.Instance;
            
            if (wgeProjectConfig == null)
            {
                result = ValidationResult.WGEConfigNotFound;
                return false;
            }
            
            if (WGEProjectConfig.Instance.IsCustomManagerEnabled)
                return IsCustomSettingsValid(wgeProjectConfig, out result);

            return IsDefaultSettingsValid(wgeProjectConfig, out result);
        }

        internal static string GetMessage(ValidationResult result)
        {
            switch (result)
            {
                case ValidationResult.Ok:
                    return "WGE data is valid.";
                case ValidationResult.WGEConfigNotFound:
                    return $"{nameof(WGEProjectConfig)} is missing from Resources. " +
                           $"Ensure {nameof(WGEProjectConfig)}.asset exists at {WGEAssetPathUtility.GetPath("Resources")}/.";
                case ValidationResult.ManagerIsNull:
                    return "The TransitionManager instance is missing. To fix this, create a new TransitionManager " +
                           "via the context menu: \"Tools/World Graph Editor/Create Transition Manager Prefab\".";
                case ValidationResult.DefaultSettingsContainerIsNull:
                    return "\"The \"Container\" field in TransitionManager is not assigned. " +
                           "Please specify a valid World Graph Container.";
                case ValidationResult.CustomSettingsContainerIsNull:
                    return "\"The \"Container\" field in WGE Project Settings is not assigned. " +
                           "Please specify a valid World Graph Container.";
                case ValidationResult.ContainerHasErrors:
                    return "The \"Container\" has validation errors. " +
                           "In the Word Graph Editor window, select the node highlighted in red to get more detailed information. " +
                           "Please review the container's data and fix the detected errors.";
                case ValidationResult.BuildSettingsMismatch:
                    return "The saved scene data does not match the scenes in the Build Settings. " +
                           "Please click the \"Refresh Build Settings\" button in the \"Container\" " +
                           "field to resolve this issue.";
                case ValidationResult.NoData:
                    return "The \"Container\" has no stored nodes or edges.";
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }

        private static bool IsCustomSettingsValid(WGEProjectConfig wgeProjectConfig, out ValidationResult result)
        {
            result = ValidationResult.Ok;
            var container = wgeProjectConfig.Container;
            
            if (container == null)
            {
                result = ValidationResult.CustomSettingsContainerIsNull;
                return false;
            }
            
            return IsContainerValid(ref result, container);
        }

        private static bool IsDefaultSettingsValid(WGEProjectConfig wgeProjectConfig, out ValidationResult result)
        {
            result = ValidationResult.Ok;
            var container = wgeProjectConfig.Container;
            
            if (TransitionManager.LoadFromResources() == null)
            {
                result = ValidationResult.ManagerIsNull;
                return false;
            }
            
            if (container == null)
            {
                result = ValidationResult.DefaultSettingsContainerIsNull;
                return false;
            }
            
            return IsContainerValid(ref result, container);
        }

        private static bool IsContainerValid(ref ValidationResult result, WorldGraphContainer container)
        {
            if (!container.HasData)
            {
                result = ValidationResult.NoData;
                return false;
            }

            if (container.HasErrors())
            {
                result = ValidationResult.ContainerHasErrors;
                return false;
            }

            if (ScenesValidationHelper.IsAllScenesValid(container)) 
                return true;
            
            result = ValidationResult.BuildSettingsMismatch;
            return false;
        }
    }
    
    internal enum ValidationResult
    {
        Ok,
        WGEConfigNotFound,
        ManagerIsNull,
        DefaultSettingsContainerIsNull,
        CustomSettingsContainerIsNull,
        ContainerHasErrors,
        BuildSettingsMismatch,
        NoData
    }
}