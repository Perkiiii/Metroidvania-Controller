using UnityEngine;

namespace WorldGraphEditor
{
    public static class Bootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Execute()
        {
            var manager = TransitionManager.LoadFromResources();

            if (manager != null && manager.AutoLoad)
                TransitionManager.CreateInstance();
        }
    }
}