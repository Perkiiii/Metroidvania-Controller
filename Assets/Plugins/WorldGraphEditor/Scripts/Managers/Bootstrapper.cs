using UnityEngine;

namespace WorldGraphEditor
{
    public static class Bootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Execute()
        {
            var resolver = WGEProjectConfig.Instance;

            if (resolver == null)
            {
                WGEConsole.Error(
                    $"{nameof(WGEProjectConfig)} not found in Resources. " +
                    $"Ensure {nameof(WGEProjectConfig)}.asset exists at Assets/WorldGraphEditor/Resources/.");
                return;
            }

            if (resolver.IsCustomManagerEnabled)
                return;

            var manager = TransitionManager.LoadFromResources();

            if (manager != null && manager.AutoLoad)
                TransitionManager.CreateInstance();
        }
    }
}
