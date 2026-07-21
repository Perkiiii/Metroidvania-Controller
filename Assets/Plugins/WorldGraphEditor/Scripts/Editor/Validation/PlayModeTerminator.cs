using UnityEditor;

namespace WorldGraphEditor.Editor
{
    [InitializeOnLoad]
    internal static class PlayModeTerminator
    {
        static PlayModeTerminator()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged; 
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;
            
            if (DataValidator.IsValid(out var result))
                return;

            var message = DataValidator.GetMessage(result);
            
            WGEConsole.Warning(message);
        }
    }
}