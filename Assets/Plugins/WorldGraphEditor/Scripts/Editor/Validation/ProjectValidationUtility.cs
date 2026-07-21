using System.Linq;
using UnityEditor;
using UnityEngine;
using WorldGraphEditor.Editor.Tests;

namespace WorldGraphEditor.Editor
{
    internal static class ProjectValidationUtility
    {
        public static bool CanValidate(WorldGraphContainer container)
        {
            return container != null && container.HasData && !container.HasErrors();
        }

        public static void RunValidationAndPingResult(WorldGraphContainer container)
        {
            var containerTests = WorldGraphEditorSettings.Instance.Tests?.Where(item => item != null);

            if (containerTests != null)
            {
                var tests = new ISceneTest[] { new MissingPortsTest(), new PortsDuplicatesTest() }.Concat(containerTests);
                var testsData = SceneCompletionValidator.GetAllSceneTestResults(container.EditorGraph.GetScenesData(), tests);

                if (testsData == null)
                    return;
                
#pragma warning disable CS0618
                container.EditorData.SetTestsData(testsData);
#pragma warning restore CS0618
            }

            var result = ProjectValidationResult.Instance;

            result.SetContainer(container);

            EditorUtility.SetDirty(result);
            EditorUtility.SetDirty(container);

            AssetDatabase.SaveAssetIfDirty(result);
            AssetDatabase.SaveAssetIfDirty(container);

            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(result);
        }
    }
}
