using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace WorldGraphEditor.Editor
{
    internal static class AffectedScenesRefresher
    {
        internal static void ForceRefresh(IEnumerable<string> scenePaths)
        {
            if (EditorApplication.isPlaying)
                return;

            var paths = scenePaths
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .ToList();

            if (paths.Count == 0)
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var originalScenePath = SceneManager.GetActiveScene().path;

            try
            {
                for (var index = 0; index < paths.Count; index++)
                {
                    var path = paths[index];

                    ProgressBar(Path.GetFileNameWithoutExtension(path), index + 1, paths.Count);

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                    TransitionComponentRefresher.RefreshPorts();
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                if (!string.IsNullOrEmpty(originalScenePath) && originalScenePath != SceneManager.GetActiveScene().path)
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }
        }

        private static void ProgressBar(string sceneName, int index, int count)
        {
            EditorUtility.DisplayProgressBar(
                    $"Ports refreshing ({index}/{count})",
                    $"Processing: {sceneName}",
                    (float) index / count);
        }

        private static bool CancellableProgressBar(string sceneName, int index, int count, ref bool cancelRequested)
        {
            if (!EditorUtility.DisplayCancelableProgressBar(
                    $"Ports refreshing ({index}/{count})",
                    $"Processing: {sceneName}",
                    (float) index / count)) 
                return false;
            
            cancelRequested = true;
            return true;
        }
    }
}
