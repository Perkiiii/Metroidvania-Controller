using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    [InitializeOnLoad]
    internal static class PlayModeTerminator
    {
        static PlayModeTerminator()
        {
            EditorApplication.update += () =>
            {
                if (!EditorApplication.isPlaying || DataValidator.IsValid(out _, out var message))
                    return;
                
                Debug.LogError(message.SetColor(MessageColor.Red));
                
                EditorApplication.isPlaying = false;
            };
        }
    }
}