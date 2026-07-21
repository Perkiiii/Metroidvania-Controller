using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldGraphEditor;

public static class WorldGraphTransitionResolver
{
    private static WorldGraphContainer container;

    public static void SetContainer(WorldGraphContainer worldGraphContainer)
    {
        container = worldGraphContainer;
    }

    public static bool TryResolve(
        string sourcePortGuid,
        bool ignoreShortcuts,
        out WorldGraphTransitionRequest request)
    {
        if (container == null)
        {
            request = new WorldGraphTransitionRequest(sourcePortGuid, "WorldGraphContainer is null.");
            return false;
        }

        if (string.IsNullOrEmpty(sourcePortGuid))
        {
            request = new WorldGraphTransitionRequest(sourcePortGuid, "Source port GUID is empty.");
            return false;
        }

        WorldGraph worldGraph;
        try
        {
            worldGraph = container.GetWorldGraph();
        }
        catch (Exception e)
        {
            request = new WorldGraphTransitionRequest(sourcePortGuid, $"WorldGraphContainer initialization threw: {e.Message}");
            return false;
        }

        if (!worldGraph.CanPassTransition(sourcePortGuid, ignoreShortcuts, out TransitionPassStatusType status))
        {
            request = new WorldGraphTransitionRequest(
                sourcePortGuid,
                $"CanPassTransition returned false. Status: {status}");
            return false;
        }

        RuntimeTransitionData data;
        try
        {
            if (!worldGraph.TryGetPassageTransitionData(sourcePortGuid, out data))
            {
                request = new WorldGraphTransitionRequest(sourcePortGuid, "TryGetPassageTransitionData returned false.");
                return false;
            }
        }
        catch (Exception e)
        {
            request = new WorldGraphTransitionRequest(sourcePortGuid, $"TryGetPassageTransitionData threw: {e.Message}");
            return false;
        }

        if (string.IsNullOrEmpty(data.TargetPassageGuid))
        {
            request = new WorldGraphTransitionRequest(sourcePortGuid, "Target passage GUID is empty.");
            return false;
        }

        string scenePath = SceneUtility.GetScenePathByBuildIndex(data.TargetSceneBuildIndex);
        string sceneName = Path.GetFileNameWithoutExtension(scenePath);

        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            request = new WorldGraphTransitionRequest(
                sourcePortGuid,
                $"Build index {data.TargetSceneBuildIndex} does not map to a loadable scene.");
            return false;
        }

        request = new WorldGraphTransitionRequest(
            sourcePortGuid,
            data.TargetPassageGuid,
            data.TargetSceneBuildIndex,
            sceneName,
            status);
        return true;
    }
}
