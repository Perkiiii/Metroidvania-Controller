#if WGE_ADDRESSABLES

using UnityEditor.AddressableAssets;

namespace WorldGraphEditor.Editor
{
    internal static class AddressablesAddressResolver
    {
        internal static string TryResolveSceneAddress(string sceneAssetGuid)
        {
            if (string.IsNullOrEmpty(sceneAssetGuid))
                return string.Empty;

            if (!AddressableAssetSettingsDefaultObject.SettingsExists)
                return string.Empty;

            var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(sceneAssetGuid);
            return entry?.address;
        }

        internal static bool IsAddressableScene(string sceneAssetGuid)
        {
            return !string.IsNullOrEmpty(TryResolveSceneAddress(sceneAssetGuid));
        }
    }
}
#endif
