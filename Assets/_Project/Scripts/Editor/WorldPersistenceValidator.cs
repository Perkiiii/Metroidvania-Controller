using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldPersistenceValidator
{
    private sealed class PersistenceInfo
    {
        public string ObjectPath;
        public string WorldObjectId;
        public EnemyPersistenceMode Mode;
        public bool HasRegistry;
        public bool HasConfig;
        public float RespawnDuration;
    }

    private sealed class PickupInfo
    {
        public string ObjectPath;
        public string WorldObjectId;
        public bool HasAbilityState;
        public bool HasRegistry;
    }

    [MenuItem("Tools/Project/Validate World Persistence")]
    public static void ValidateWorldPersistence()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[WorldPersistenceValidator] Validation cancelled because modified scenes were not saved.");
            return;
        }

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        Dictionary<string, string> enabledScenesByName = GetEnabledBuildScenesByName();
        Dictionary<string, List<PersistenceInfo>> infosByScene = new Dictionary<string, List<PersistenceInfo>>();
        Dictionary<string, List<PickupInfo>> pickupInfosByScene = new Dictionary<string, List<PickupInfo>>();
        int issueCount = 0;

        try
        {
            foreach (KeyValuePair<string, string> sceneEntry in enabledScenesByName)
            {
                Scene scene = EditorSceneManager.OpenScene(sceneEntry.Value, OpenSceneMode.Single);

                List<PersistenceInfo> infos = CollectPersistenceInfo(scene);
                infosByScene[sceneEntry.Key] = infos;
                issueCount += ValidateSceneLocal(sceneEntry.Key, infos);

                List<PickupInfo> pickupInfos = CollectPickupInfo(scene);
                pickupInfosByScene[sceneEntry.Key] = pickupInfos;
                issueCount += ValidatePickupsLocal(sceneEntry.Key, pickupInfos);
            }
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        issueCount += ValidateGlobalIdUniqueness(infosByScene);
        issueCount += ValidatePickupGlobalIdUniqueness(pickupInfosByScene);

        int totalInstances = 0;
        foreach (List<PersistenceInfo> infos in infosByScene.Values)
            totalInstances += infos.Count;

        int totalPickups = 0;
        foreach (List<PickupInfo> infos in pickupInfosByScene.Values)
            totalPickups += infos.Count;

        if (issueCount == 0)
            Debug.Log($"[WorldPersistenceValidator] Checked {enabledScenesByName.Count} enabled Build Settings scene(s), {totalInstances} EnemyPersistence instance(s), {totalPickups} AbilityPickup instance(s). No issues found.");
        else
            Debug.LogWarning($"[WorldPersistenceValidator] Checked {enabledScenesByName.Count} enabled Build Settings scene(s), {totalInstances} EnemyPersistence instance(s), {totalPickups} AbilityPickup instance(s). Found {issueCount} issue(s). See earlier logs.");
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
                continue;

            scenes.Add(sceneName, buildScene.path);
        }

        return scenes;
    }

    private static List<PersistenceInfo> CollectPersistenceInfo(Scene scene)
    {
        EnemyPersistence[] instances = Object.FindObjectsByType<EnemyPersistence>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<PersistenceInfo> result = new List<PersistenceInfo>();

        foreach (EnemyPersistence instance in instances)
        {
            if (instance == null || instance.gameObject.scene != scene)
                continue;

            SerializedObject serialized = new SerializedObject(instance);
            string worldObjectId = serialized.FindProperty("worldObjectId")?.stringValue ?? "";
            WorldStateRegistry registry = serialized.FindProperty("registry")?.objectReferenceValue as WorldStateRegistry;

            EnemyController controller = instance.GetComponent<EnemyController>();
            EnemyConfig config = controller != null ? controller.Config : null;

            result.Add(new PersistenceInfo
            {
                ObjectPath = GetHierarchyPath(instance.transform),
                WorldObjectId = worldObjectId,
                Mode = instance.Mode,
                HasRegistry = registry != null,
                HasConfig = config != null,
                RespawnDuration = config != null ? config.respawnDuration : 0f
            });
        }

        return result;
    }

    private static int ValidateSceneLocal(string sceneName, List<PersistenceInfo> infos)
    {
        int issues = 0;
        Dictionary<string, PersistenceInfo> firstById = new Dictionary<string, PersistenceInfo>();

        foreach (PersistenceInfo info in infos)
        {
            if (string.IsNullOrWhiteSpace(info.WorldObjectId))
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' EnemyPersistence '{info.ObjectPath}' has no worldObjectId assigned.");
                issues++;
            }
            else if (firstById.TryGetValue(info.WorldObjectId, out PersistenceInfo first))
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' has duplicate worldObjectId '{info.WorldObjectId}' at '{first.ObjectPath}' and '{info.ObjectPath}' — likely a duplicated prefab instance whose ID was not changed.");
                issues++;
            }
            else
            {
                firstById.Add(info.WorldObjectId, info);
            }

            if (!info.HasRegistry)
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' EnemyPersistence '{info.ObjectPath}' has no WorldStateRegistry assigned.");
                issues++;
            }

            if (!info.HasConfig)
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' EnemyPersistence '{info.ObjectPath}' has no EnemyConfig on its EnemyController.");
                issues++;
            }
            else if (info.Mode == EnemyPersistenceMode.RespawnableTimed && info.RespawnDuration <= 0f)
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' EnemyPersistence '{info.ObjectPath}' is RespawnableTimed but its EnemyConfig.respawnDuration is not greater than 0.");
                issues++;
            }
        }

        return issues;
    }

    private static List<PickupInfo> CollectPickupInfo(Scene scene)
    {
        AbilityPickup[] instances = Object.FindObjectsByType<AbilityPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<PickupInfo> result = new List<PickupInfo>();

        foreach (AbilityPickup instance in instances)
        {
            if (instance == null || instance.gameObject.scene != scene)
                continue;

            SerializedObject serialized = new SerializedObject(instance);
            string worldObjectId = serialized.FindProperty("worldObjectId")?.stringValue ?? "";
            bool hasAbilityState = serialized.FindProperty("abilityState")?.objectReferenceValue != null;
            bool hasRegistry = serialized.FindProperty("registry")?.objectReferenceValue != null;

            result.Add(new PickupInfo
            {
                ObjectPath = GetHierarchyPath(instance.transform),
                WorldObjectId = worldObjectId,
                HasAbilityState = hasAbilityState,
                HasRegistry = hasRegistry
            });
        }

        return result;
    }

    private static int ValidatePickupsLocal(string sceneName, List<PickupInfo> infos)
    {
        int issues = 0;
        Dictionary<string, PickupInfo> firstById = new Dictionary<string, PickupInfo>();

        foreach (PickupInfo info in infos)
        {
            if (string.IsNullOrWhiteSpace(info.WorldObjectId))
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' AbilityPickup '{info.ObjectPath}' has no worldObjectId assigned.");
                issues++;
            }
            else if (firstById.TryGetValue(info.WorldObjectId, out PickupInfo first))
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' has duplicate pickup worldObjectId '{info.WorldObjectId}' at '{first.ObjectPath}' and '{info.ObjectPath}' — likely a duplicated prefab instance whose ID was not changed.");
                issues++;
            }
            else
            {
                firstById.Add(info.WorldObjectId, info);
            }

            if (!info.HasAbilityState)
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' AbilityPickup '{info.ObjectPath}' has no PlayerAbilityState assigned.");
                issues++;
            }

            if (!info.HasRegistry)
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' AbilityPickup '{info.ObjectPath}' has no WorldStateRegistry assigned.");
                issues++;
            }
        }

        return issues;
    }

    private static int ValidatePickupGlobalIdUniqueness(Dictionary<string, List<PickupInfo>> infosByScene)
    {
        int issues = 0;
        Dictionary<string, (string Scene, string Path)> firstById = new Dictionary<string, (string, string)>();

        foreach (KeyValuePair<string, List<PickupInfo>> sceneEntry in infosByScene)
        {
            foreach (PickupInfo info in sceneEntry.Value)
            {
                if (string.IsNullOrWhiteSpace(info.WorldObjectId))
                    continue;

                if (firstById.TryGetValue(info.WorldObjectId, out (string Scene, string Path) first))
                {
                    if (first.Scene == sceneEntry.Key)
                        continue; // already reported by ValidatePickupsLocal

                    Debug.LogError($"[WorldPersistenceValidator] Pickup worldObjectId '{info.WorldObjectId}' is used in both '{first.Scene}' ('{first.Path}') and '{sceneEntry.Key}' ('{info.ObjectPath}'). World object IDs must be globally unique.");
                    issues++;
                }
                else
                {
                    firstById.Add(info.WorldObjectId, (sceneEntry.Key, info.ObjectPath));
                }
            }
        }

        return issues;
    }

    private static int ValidateGlobalIdUniqueness(Dictionary<string, List<PersistenceInfo>> infosByScene)
    {
        int issues = 0;
        Dictionary<string, (string Scene, string Path)> firstById = new Dictionary<string, (string, string)>();

        foreach (KeyValuePair<string, List<PersistenceInfo>> sceneEntry in infosByScene)
        {
            foreach (PersistenceInfo info in sceneEntry.Value)
            {
                if (string.IsNullOrWhiteSpace(info.WorldObjectId))
                    continue;

                if (firstById.TryGetValue(info.WorldObjectId, out (string Scene, string Path) first))
                {
                    if (first.Scene == sceneEntry.Key)
                        continue; // already reported by ValidateSceneLocal

                    Debug.LogError($"[WorldPersistenceValidator] worldObjectId '{info.WorldObjectId}' is used in both '{first.Scene}' ('{first.Path}') and '{sceneEntry.Key}' ('{info.ObjectPath}'). World object IDs must be globally unique.");
                    issues++;
                }
                else
                {
                    firstById.Add(info.WorldObjectId, (sceneEntry.Key, info.ObjectPath));
                }
            }
        }

        return issues;
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
