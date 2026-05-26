using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TransitionGateLinkValidator
{
    private sealed class GateInfo
    {
        public string ObjectPath;
        public TransitionPoint Gate;
    }

    [MenuItem("Tools/Project/Validate Transition Gate Links")]
    public static void ValidateTransitionGateLinks()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[TransitionGateLinkValidator] Validation cancelled because modified scenes were not saved.");
            return;
        }

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        Dictionary<string, string> enabledScenes = GetEnabledBuildScenesByName();
        Dictionary<string, List<GateInfo>> gatesByScene = new Dictionary<string, List<GateInfo>>();
        int issueCount = 0;

        try
        {
            foreach (KeyValuePair<string, string> sceneEntry in enabledScenes)
            {
                Scene scene = EditorSceneManager.OpenScene(sceneEntry.Value, OpenSceneMode.Single);
                List<GateInfo> gates = CollectTransitionPoints(scene);
                gatesByScene[sceneEntry.Key] = gates;
                issueCount += ValidateLocalGateKeys(sceneEntry.Key, gates);
            }

            foreach (KeyValuePair<string, List<GateInfo>> sceneEntry in gatesByScene)
            {
                issueCount += ValidateSourceLinks(sceneEntry.Key, sceneEntry.Value, enabledScenes, gatesByScene);
            }
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        if (issueCount == 0)
            Debug.Log($"[TransitionGateLinkValidator] Checked {enabledScenes.Count} enabled Build Settings scene(s). No transition gate link issues found.");
        else
            Debug.LogWarning($"[TransitionGateLinkValidator] Checked {enabledScenes.Count} enabled Build Settings scene(s). Found {issueCount} transition gate link issue(s). See earlier logs.");
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
                Gate = gate
            });
        }

        return result;
    }

    private static int ValidateLocalGateKeys(string sceneName, List<GateInfo> gates)
    {
        int issues = 0;
        Dictionary<string, GateInfo> firstByKey = new Dictionary<string, GateInfo>();

        foreach (GateInfo gate in gates)
        {
            string key = gate.Gate.GateKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogError($"[TransitionGateLinkValidator] Scene '{sceneName}' has TransitionPoint with blank gateKey at '{gate.ObjectPath}'.", gate.Gate);
                issues++;
                continue;
            }

            if (firstByKey.TryGetValue(key, out GateInfo first))
            {
                Debug.LogError($"[TransitionGateLinkValidator] Scene '{sceneName}' has duplicate gateKey '{key}' at '{first.ObjectPath}' and '{gate.ObjectPath}'.", gate.Gate);
                issues++;
                continue;
            }

            firstByKey.Add(key, gate);
        }

        return issues;
    }

    private static int ValidateSourceLinks(
        string sceneName,
        List<GateInfo> gates,
        Dictionary<string, string> enabledScenes,
        Dictionary<string, List<GateInfo>> gatesByScene)
    {
        int issues = 0;

        foreach (GateInfo gate in gates)
        {
            string targetScene = gate.Gate.TargetScene;
            if (string.IsNullOrWhiteSpace(targetScene))
                continue;

            if (!enabledScenes.ContainsKey(targetScene))
            {
                Debug.LogError($"[TransitionGateLinkValidator] Scene '{sceneName}' gate '{gate.ObjectPath}' targets scene '{targetScene}', which is not enabled in Build Settings.", gate.Gate);
                issues++;
                continue;
            }

            string entryGateKey = gate.Gate.EntryGateKey;
            if (string.IsNullOrWhiteSpace(entryGateKey))
            {
                Debug.LogError($"[TransitionGateLinkValidator] Scene '{sceneName}' gate '{gate.ObjectPath}' targets '{targetScene}' but has blank entryGateKey.", gate.Gate);
                issues++;
                continue;
            }

            int matches = CountMatchingGateKey(gatesByScene[targetScene], entryGateKey);
            if (matches != 1)
            {
                Debug.LogError($"[TransitionGateLinkValidator] Scene '{sceneName}' gate '{gate.ObjectPath}' targets '{targetScene}' entryGateKey '{entryGateKey}', but target scene contains {matches} matching gate(s). Expected exactly 1.", gate.Gate);
                issues++;
            }
        }

        return issues;
    }

    private static int CountMatchingGateKey(List<GateInfo> gates, string gateKey)
    {
        int count = 0;
        foreach (GateInfo gate in gates)
        {
            if (gate.Gate.GateKey == gateKey)
                count++;
        }

        return count;
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
