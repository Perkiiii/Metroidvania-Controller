using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldGraphEditor;

public static class TransitionGateLinkValidator
{
    private sealed class GateInfo
    {
        public string ObjectPath;
        public string PassageGuid;
        // No live TransitionPoint reference. GateInfo objects are collected while each scene is
        // open but consumed in a second pass after OpenSceneMode.Single has replaced those scenes,
        // making any stored MonoBehaviour references stale. ObjectPath and PassageGuid are strings
        // so they survive across scene loads.
    }

    [MenuItem("Tools/Project/Validate Transition Gate Links")]
    public static void ValidateTransitionGateLinks()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[TransitionGateLinkValidator] Validation cancelled because modified scenes were not saved.");
            return;
        }

        WorldGraphContainer container = LoadWorldGraphContainer();
        if (container == null)
        {
            Debug.LogError("[TransitionGateLinkValidator] No WorldGraphContainer is assigned on Assets/WorldGraphEditor/Resources/TransitionManager.prefab.");
            return;
        }

        try
        {
            if (!container.IsInitialized())
                container.Initialize();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TransitionGateLinkValidator] WorldGraphContainer initialization failed: {e.Message}", container);
            return;
        }

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        Dictionary<string, string> enabledScenesByName = GetEnabledBuildScenesByName();
        Dictionary<int, string> enabledScenesByBuildIndex = GetEnabledBuildScenesByBuildIndex();
        Dictionary<string, List<GateInfo>> gatesByScene = new Dictionary<string, List<GateInfo>>();
        int issueCount = 0;

        try
        {
            foreach (KeyValuePair<string, string> sceneEntry in enabledScenesByName)
            {
                Scene scene = EditorSceneManager.OpenScene(sceneEntry.Value, OpenSceneMode.Single);
                List<GateInfo> gates = CollectTransitionPoints(scene);
                gatesByScene[sceneEntry.Key] = gates;
                issueCount += ValidateLocalPassageGuids(sceneEntry.Key, gates);
            }

            foreach (KeyValuePair<string, List<GateInfo>> sceneEntry in gatesByScene)
            {
                issueCount += ValidateSourceLinks(
                    sceneEntry.Key,
                    sceneEntry.Value,
                    enabledScenesByBuildIndex,
                    gatesByScene,
                    container);
            }
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        if (issueCount == 0)
            Debug.Log($"[TransitionGateLinkValidator] Checked {enabledScenesByName.Count} enabled Build Settings scene(s). No transition gate link issues found.");
        else
            Debug.LogWarning($"[TransitionGateLinkValidator] Checked {enabledScenesByName.Count} enabled Build Settings scene(s). Found {issueCount} transition gate link issue(s). See earlier logs.");
    }

    private static WorldGraphContainer LoadWorldGraphContainer()
    {
        WorldGraphEditor.TransitionManager manager = WorldGraphEditor.TransitionManager.LoadFromResources();
        return manager != null ? manager.Container : null;
    }

    private static Dictionary<string, string> GetEnabledBuildScenesByName()
    {
        Dictionary<string, string> scenes = new Dictionary<string, string>();
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path))
                continue;

            string sceneName = Path.GetFileNameWithoutExtension(buildScene.path);
            if (scenes.ContainsKey(sceneName))
            {
                Debug.LogWarning($"[TransitionGateLinkValidator] Multiple enabled Build Settings scenes are named '{sceneName}'. Gate validation will use '{scenes[sceneName]}' and ignore '{buildScene.path}'.");
                continue;
            }

            scenes.Add(sceneName, buildScene.path);
        }

        return scenes;
    }

    private static Dictionary<int, string> GetEnabledBuildScenesByBuildIndex()
    {
        // Build a lookup of enabled scene paths so we can filter out disabled scenes.
        HashSet<string> enabledPaths = new HashSet<string>();
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (buildScene.enabled && !string.IsNullOrWhiteSpace(buildScene.path))
                enabledPaths.Add(buildScene.path);
        }

        // Use SceneUtility.GetScenePathByBuildIndex to produce a map that matches Unity's actual
        // positional build index system (all scenes in the Build Settings list are indexed by
        // position, including disabled ones). WGE stores these positional indices in
        // SceneRuntimeData.BuildIndex. Counting only enabled scenes would produce wrong indices
        // if any disabled scene appears before an enabled one in the Build Settings list.
        Dictionary<int, string> scenes = new Dictionary<int, string>();
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (!string.IsNullOrWhiteSpace(path) && enabledPaths.Contains(path))
                scenes.Add(i, path);
        }

        return scenes;
    }

    private static List<GateInfo> CollectTransitionPoints(Scene scene)
    {
        TransitionPoint[] gates = Object.FindObjectsByType<TransitionPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<GateInfo> result = new List<GateInfo>();

        foreach (TransitionPoint gate in gates)
        {
            if (gate == null || gate.gameObject.scene != scene)
                continue;

            result.Add(new GateInfo
            {
                ObjectPath = GetHierarchyPath(gate.transform),
                PassageGuid = GetSerializedPassageGuid(gate)
            });
        }

        return result;
    }

    private static int ValidateLocalPassageGuids(string sceneName, List<GateInfo> gates)
    {
        int issues = 0;
        Dictionary<string, GateInfo> firstByGuid = new Dictionary<string, GateInfo>();

        foreach (GateInfo gate in gates)
        {
            string guid = gate.PassageGuid;
            if (string.IsNullOrWhiteSpace(guid))
            {
                Debug.LogError($"[TransitionGateLinkValidator] Scene '{sceneName}' gate '{gate.ObjectPath}' has no WGE port assigned.");
                issues++;
                continue;
            }

            if (firstByGuid.TryGetValue(guid, out GateInfo first))
            {
                Debug.LogError($"[TransitionGateLinkValidator] Scene '{sceneName}' has duplicate WGE passage GUID '{guid}' at '{first.ObjectPath}' and '{gate.ObjectPath}'.");
                issues++;
                continue;
            }

            firstByGuid.Add(guid, gate);
        }

        return issues;
    }

    private static int ValidateSourceLinks(
        string sceneName,
        List<GateInfo> gates,
        Dictionary<int, string> enabledScenesByBuildIndex,
        Dictionary<string, List<GateInfo>> gatesByScene,
        WorldGraphContainer container)
    {
        int issues = 0;

        foreach (GateInfo gate in gates)
        {
            string guid = gate.PassageGuid;
            if (string.IsNullOrWhiteSpace(guid))
                continue;

            if (!container.CanPassTransition(guid, false, out TransitionPassStatusType status))
            {
                Debug.LogError($"[TransitionGateLinkValidator] Scene '{sceneName}' gate '{gate.ObjectPath}' cannot pass WGE transition for GUID '{guid}'. Status: {status}.");
                issues++;
                continue;
            }

            RuntimeTransitionData data;
            try
            {
                data = container.GetTransitionData(guid, false);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TransitionGateLinkValidator] Scene '{sceneName}' gate '{gate.ObjectPath}' failed WGE transition lookup for GUID '{guid}': {e.Message}");
                issues++;
                continue;
            }

            if (!enabledScenesByBuildIndex.TryGetValue(data.TargetSceneBuildIndex, out string targetScenePath))
            {
                Debug.LogError($"[TransitionGateLinkValidator] Scene '{sceneName}' gate '{gate.ObjectPath}' targets build index {data.TargetSceneBuildIndex}, which is not enabled in Build Settings.");
                issues++;
                continue;
            }

            string targetSceneName = Path.GetFileNameWithoutExtension(targetScenePath);
            if (!gatesByScene.TryGetValue(targetSceneName, out List<GateInfo> targetGates))
            {
                Debug.LogError($"[TransitionGateLinkValidator] Scene '{sceneName}' gate '{gate.ObjectPath}' targets scene '{targetSceneName}', but that scene was not scanned.");
                issues++;
                continue;
            }

            int matches = CountMatchingPassageGuid(targetGates, data.TargetPassageGuid);
            if (matches != 1)
            {
                Debug.LogError($"[TransitionGateLinkValidator] Scene '{sceneName}' gate '{gate.ObjectPath}' targets passage GUID '{data.TargetPassageGuid}' in '{targetSceneName}', but target scene contains {matches} matching gate(s). Expected exactly 1.");
                issues++;
            }
        }

        return issues;
    }

    private static int CountMatchingPassageGuid(List<GateInfo> gates, string passageGuid)
    {
        int count = 0;
        foreach (GateInfo gate in gates)
        {
            if (gate.PassageGuid == passageGuid)
                count++;
        }

        return count;
    }

    private static string GetSerializedPassageGuid(TransitionPoint gate)
    {
        if (gate == null)
            return "";

        SerializedObject serializedGate = new SerializedObject(gate);
        SerializedProperty assignedPort = serializedGate.FindProperty("_assignedPort");
        SerializedProperty selectedGuid = assignedPort?.FindPropertyRelative("_selectedGuid");
        return selectedGuid != null ? selectedGuid.stringValue : "";
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return "<null>";

        string path = transform.name;
        Transform parent = transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}
