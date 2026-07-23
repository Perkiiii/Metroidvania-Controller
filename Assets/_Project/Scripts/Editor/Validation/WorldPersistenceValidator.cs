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

    private sealed class DoorInfo
    {
        public string ObjectPath;
        public string WorldObjectId;
        public bool HasRegistry;
        public bool HasBlockerCollider;
    }

    private sealed class SwitchInfo
    {
        public string ObjectPath;
        public string WorldObjectId;
        public bool HasRegistry;
        public bool HasBlockerCollider;
        public PersistenceLifetime Lifetime;
    }

    private sealed class BreakableInfo
    {
        public string ObjectPath;
        public string WorldObjectId;
        public bool HasRegistry;
        public bool HasSolidCollider;
        public PersistenceLifetime Lifetime;
    }

    private readonly struct GlobalIdEntry
    {
        public readonly string Id;
        public readonly string TypeName;
        public readonly string Scene;
        public readonly string Path;

        public GlobalIdEntry(string id, string typeName, string scene, string path)
        {
            Id = id;
            TypeName = typeName;
            Scene = scene;
            Path = path;
        }
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
        Dictionary<string, string> enabledScenesByName = GetEnabledBuildScenesByName(out int duplicateSceneNameIssues);

        Dictionary<string, List<PersistenceInfo>> infosByScene = new Dictionary<string, List<PersistenceInfo>>();
        Dictionary<string, List<PickupInfo>> pickupInfosByScene = new Dictionary<string, List<PickupInfo>>();
        Dictionary<string, List<DoorInfo>> doorInfosByScene = new Dictionary<string, List<DoorInfo>>();
        Dictionary<string, List<SwitchInfo>> switchInfosByScene = new Dictionary<string, List<SwitchInfo>>();
        Dictionary<string, List<BreakableInfo>> breakableInfosByScene = new Dictionary<string, List<BreakableInfo>>();
        int issueCount = duplicateSceneNameIssues;

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

                List<DoorInfo> doorInfos = CollectDoorInfo(scene);
                doorInfosByScene[sceneEntry.Key] = doorInfos;
                issueCount += ValidateDoorsLocal(sceneEntry.Key, doorInfos);

                List<SwitchInfo> switchInfos = CollectSwitchInfo(scene);
                switchInfosByScene[sceneEntry.Key] = switchInfos;
                issueCount += ValidateSwitchesLocal(sceneEntry.Key, switchInfos);

                List<BreakableInfo> breakableInfos = CollectBreakableInfo(scene);
                breakableInfosByScene[sceneEntry.Key] = breakableInfos;
                issueCount += ValidateBreakablesLocal(sceneEntry.Key, breakableInfos);

                issueCount += ValidateNoConflictingParticipants(scene);
            }
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        List<GlobalIdEntry> allEntries = new List<GlobalIdEntry>();
        AppendEntries(allEntries, infosByScene, "EnemyPersistence", info => info.WorldObjectId, info => info.ObjectPath);
        AppendEntries(allEntries, pickupInfosByScene, "AbilityPickup", info => info.WorldObjectId, info => info.ObjectPath);
        AppendEntries(allEntries, doorInfosByScene, "PersistentDoor", info => info.WorldObjectId, info => info.ObjectPath);
        AppendEntries(allEntries, switchInfosByScene, "PersistentSwitch", info => info.WorldObjectId, info => info.ObjectPath);
        AppendEntries(allEntries, breakableInfosByScene, "PersistentBreakable", info => info.WorldObjectId, info => info.ObjectPath);
        issueCount += ValidateUnifiedGlobalIdUniqueness(allEntries);

        int totalInstances = CountAll(infosByScene);
        int totalPickups = CountAll(pickupInfosByScene);
        int totalDoors = CountAll(doorInfosByScene);
        int totalSwitches = CountAll(switchInfosByScene);
        int totalBreakables = CountAll(breakableInfosByScene);

        string summary = $"Checked {enabledScenesByName.Count} enabled Build Settings scene(s), " +
            $"{totalInstances} EnemyPersistence, {totalPickups} AbilityPickup, {totalDoors} PersistentDoor, " +
            $"{totalSwitches} PersistentSwitch, {totalBreakables} PersistentBreakable instance(s).";

        if (issueCount == 0)
            Debug.Log($"[WorldPersistenceValidator] {summary} No issues found.");
        else
            Debug.LogWarning($"[WorldPersistenceValidator] {summary} Found {issueCount} issue(s). See earlier logs.");
    }

    private static int CountAll<T>(Dictionary<string, List<T>> infosByScene)
    {
        int total = 0;
        foreach (List<T> infos in infosByScene.Values)
            total += infos.Count;
        return total;
    }

    private static void AppendEntries<T>(
        List<GlobalIdEntry> target,
        Dictionary<string, List<T>> infosByScene,
        string typeName,
        System.Func<T, string> idSelector,
        System.Func<T, string> pathSelector)
    {
        foreach (KeyValuePair<string, List<T>> sceneEntry in infosByScene)
        {
            foreach (T info in sceneEntry.Value)
            {
                string id = idSelector(info);
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                target.Add(new GlobalIdEntry(id, typeName, sceneEntry.Key, pathSelector(info)));
            }
        }
    }

    // A pure same-scene, same-type duplicate ID set is already reported by that type's own local
    // per-scene validator -- this pass exists to catch everything that is NOT already covered:
    // same-scene-different-type collisions, and any cross-scene collision for the new participant
    // types (EnemyPersistence/AbilityPickup cross-scene collisions were already covered by the two
    // prior separate passes this replaces; doors/switches/breakables previously had no cross-scene
    // check at all).
    private static int ValidateUnifiedGlobalIdUniqueness(List<GlobalIdEntry> entries)
    {
        int issues = 0;
        Dictionary<string, List<GlobalIdEntry>> byId = new Dictionary<string, List<GlobalIdEntry>>();

        foreach (GlobalIdEntry entry in entries)
        {
            if (!byId.TryGetValue(entry.Id, out List<GlobalIdEntry> list))
            {
                list = new List<GlobalIdEntry>();
                byId[entry.Id] = list;
            }

            list.Add(entry);
        }

        foreach (KeyValuePair<string, List<GlobalIdEntry>> group in byId)
        {
            List<GlobalIdEntry> occurrences = group.Value;
            if (occurrences.Count <= 1)
                continue;

            bool allSameSceneAndType = true;
            for (int i = 1; i < occurrences.Count; i++)
            {
                if (occurrences[i].Scene != occurrences[0].Scene || occurrences[i].TypeName != occurrences[0].TypeName)
                {
                    allSameSceneAndType = false;
                    break;
                }
            }

            if (allSameSceneAndType)
                continue;

            List<string> locations = new List<string>();
            foreach (GlobalIdEntry occurrence in occurrences)
                locations.Add($"{occurrence.TypeName} at '{occurrence.Scene}'/'{occurrence.Path}'");

            Debug.LogError($"[WorldPersistenceValidator] worldObjectId '{group.Key}' is used by multiple persistent world objects: {string.Join(", ", locations)}. World object IDs must be globally unique across every persistent world object type (enemies, pickups, doors, switches, breakables).");
            issues++;
        }

        return issues;
    }

    private static int ValidateNoConflictingParticipants(Scene scene)
    {
        int issues = 0;
        HashSet<GameObject> visited = new HashSet<GameObject>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                if (!visited.Add(go))
                    continue;

                int count = 0;
                if (go.GetComponent<EnemyPersistence>() != null) count++;
                if (go.GetComponent<AbilityPickup>() != null) count++;
                if (go.GetComponent<PersistentDoor>() != null) count++;
                if (go.GetComponent<PersistentSwitch>() != null) count++;
                if (go.GetComponent<PersistentBreakable>() != null) count++;

                if (count > 1)
                {
                    Debug.LogError($"[WorldPersistenceValidator] Scene '{scene.name}' GameObject '{GetHierarchyPath(t)}' has {count} persistence participant components on one object. Each object may host only one persistence participant type.");
                    issues++;
                }
            }
        }

        return issues;
    }

    private static Dictionary<string, string> GetEnabledBuildScenesByName(out int duplicateSceneNameIssues)
    {
        Dictionary<string, string> scenes = new Dictionary<string, string>();
        duplicateSceneNameIssues = 0;

        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path))
                continue;

            string sceneName = Path.GetFileNameWithoutExtension(buildScene.path);
            if (scenes.ContainsKey(sceneName))
            {
                Debug.LogError($"[WorldPersistenceValidator] Two enabled Build Settings scenes share the name '{sceneName}' (one at '{buildScene.path}'). Scene name is the room-visitation ID used by GameManager/WorldStateRegistry.MarkRoomVisited -- duplicate names would make two different rooms register as the same visited room. Rename one of the scene files.");
                duplicateSceneNameIssues++;
                continue;
            }

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

    private static List<DoorInfo> CollectDoorInfo(Scene scene)
    {
        PersistentDoor[] instances = Object.FindObjectsByType<PersistentDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<DoorInfo> result = new List<DoorInfo>();

        foreach (PersistentDoor instance in instances)
        {
            if (instance == null || instance.gameObject.scene != scene)
                continue;

            SerializedObject serialized = new SerializedObject(instance);
            string worldObjectId = serialized.FindProperty("worldObjectId")?.stringValue ?? "";
            bool hasRegistry = serialized.FindProperty("registry")?.objectReferenceValue != null;
            bool hasBlocker = serialized.FindProperty("blockerCollider")?.objectReferenceValue != null;

            result.Add(new DoorInfo
            {
                ObjectPath = GetHierarchyPath(instance.transform),
                WorldObjectId = worldObjectId,
                HasRegistry = hasRegistry,
                HasBlockerCollider = hasBlocker
            });
        }

        return result;
    }

    private static int ValidateDoorsLocal(string sceneName, List<DoorInfo> infos)
    {
        int issues = 0;
        Dictionary<string, DoorInfo> firstById = new Dictionary<string, DoorInfo>();

        foreach (DoorInfo info in infos)
        {
            if (string.IsNullOrWhiteSpace(info.WorldObjectId))
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' PersistentDoor '{info.ObjectPath}' has no worldObjectId assigned. Assign a unique, stable ID.");
                issues++;
            }
            else if (firstById.TryGetValue(info.WorldObjectId, out DoorInfo first))
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' has duplicate PersistentDoor worldObjectId '{info.WorldObjectId}' at '{first.ObjectPath}' and '{info.ObjectPath}' — likely a duplicated prefab instance whose ID was not changed.");
                issues++;
            }
            else
            {
                firstById.Add(info.WorldObjectId, info);
            }

            if (!info.HasRegistry)
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' PersistentDoor '{info.ObjectPath}' has no WorldStateRegistry assigned. Assign the project's WorldStateRegistry asset.");
                issues++;
            }

            if (!info.HasBlockerCollider)
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' PersistentDoor '{info.ObjectPath}' has no blockerCollider assigned — opening it would have no physical effect. Assign the solid collider this door blocks passage with.");
                issues++;
            }
        }

        return issues;
    }

    private static List<SwitchInfo> CollectSwitchInfo(Scene scene)
    {
        PersistentSwitch[] instances = Object.FindObjectsByType<PersistentSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<SwitchInfo> result = new List<SwitchInfo>();

        foreach (PersistentSwitch instance in instances)
        {
            if (instance == null || instance.gameObject.scene != scene)
                continue;

            SerializedObject serialized = new SerializedObject(instance);
            string worldObjectId = serialized.FindProperty("worldObjectId")?.stringValue ?? "";
            bool hasRegistry = serialized.FindProperty("registry")?.objectReferenceValue != null;
            bool hasBlocker = serialized.FindProperty("blockerCollider")?.objectReferenceValue != null;
            SerializedProperty lifetimeProp = serialized.FindProperty("lifetime");
            PersistenceLifetime lifetime = lifetimeProp != null ? (PersistenceLifetime)lifetimeProp.enumValueIndex : PersistenceLifetime.RoomRuntime;

            result.Add(new SwitchInfo
            {
                ObjectPath = GetHierarchyPath(instance.transform),
                WorldObjectId = worldObjectId,
                HasRegistry = hasRegistry,
                HasBlockerCollider = hasBlocker,
                Lifetime = lifetime
            });
        }

        return result;
    }

    private static int ValidateSwitchesLocal(string sceneName, List<SwitchInfo> infos)
    {
        int issues = 0;
        Dictionary<string, SwitchInfo> firstById = new Dictionary<string, SwitchInfo>();

        foreach (SwitchInfo info in infos)
        {
            bool needsId = info.Lifetime != PersistenceLifetime.RoomRuntime;

            if (needsId && string.IsNullOrWhiteSpace(info.WorldObjectId))
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' PersistentSwitch '{info.ObjectPath}' has lifetime {info.Lifetime} but no worldObjectId assigned. Assign a unique, stable ID or switch its lifetime to RoomRuntime.");
                issues++;
            }
            else if (!string.IsNullOrWhiteSpace(info.WorldObjectId))
            {
                if (firstById.TryGetValue(info.WorldObjectId, out SwitchInfo first))
                {
                    Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' has duplicate PersistentSwitch worldObjectId '{info.WorldObjectId}' at '{first.ObjectPath}' and '{info.ObjectPath}' — likely a duplicated prefab instance whose ID was not changed.");
                    issues++;
                }
                else
                {
                    firstById.Add(info.WorldObjectId, info);
                }

                if (!needsId)
                {
                    Debug.LogWarning($"[WorldPersistenceValidator] Scene '{sceneName}' PersistentSwitch '{info.ObjectPath}' has lifetime RoomRuntime but a worldObjectId is assigned. RoomRuntime never reads or writes the registry; clear the ID or change the lifetime.");
                }
            }

            if (needsId && !info.HasRegistry)
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' PersistentSwitch '{info.ObjectPath}' has lifetime {info.Lifetime} but no WorldStateRegistry assigned.");
                issues++;
            }

            if (!info.HasBlockerCollider)
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' PersistentSwitch '{info.ObjectPath}' has no blockerCollider assigned — activating it would have no physical effect. Assign the paired gate/blocker collider.");
                issues++;
            }
        }

        return issues;
    }

    private static List<BreakableInfo> CollectBreakableInfo(Scene scene)
    {
        PersistentBreakable[] instances = Object.FindObjectsByType<PersistentBreakable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<BreakableInfo> result = new List<BreakableInfo>();

        foreach (PersistentBreakable instance in instances)
        {
            if (instance == null || instance.gameObject.scene != scene)
                continue;

            SerializedObject serialized = new SerializedObject(instance);
            string worldObjectId = serialized.FindProperty("worldObjectId")?.stringValue ?? "";
            bool hasRegistry = serialized.FindProperty("registry")?.objectReferenceValue != null;
            bool hasSolid = serialized.FindProperty("solidCollider")?.objectReferenceValue != null;
            SerializedProperty lifetimeProp = serialized.FindProperty("lifetime");
            PersistenceLifetime lifetime = lifetimeProp != null ? (PersistenceLifetime)lifetimeProp.enumValueIndex : PersistenceLifetime.RoomRuntime;

            result.Add(new BreakableInfo
            {
                ObjectPath = GetHierarchyPath(instance.transform),
                WorldObjectId = worldObjectId,
                HasRegistry = hasRegistry,
                HasSolidCollider = hasSolid,
                Lifetime = lifetime
            });
        }

        return result;
    }

    private static int ValidateBreakablesLocal(string sceneName, List<BreakableInfo> infos)
    {
        int issues = 0;
        Dictionary<string, BreakableInfo> firstById = new Dictionary<string, BreakableInfo>();

        foreach (BreakableInfo info in infos)
        {
            bool needsId = info.Lifetime != PersistenceLifetime.RoomRuntime;

            if (needsId && string.IsNullOrWhiteSpace(info.WorldObjectId))
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' PersistentBreakable '{info.ObjectPath}' has lifetime {info.Lifetime} but no worldObjectId assigned — a persistent breakable needs a stable ID to survive scene reload. Assign one or switch its lifetime to RoomRuntime.");
                issues++;
            }
            else if (!string.IsNullOrWhiteSpace(info.WorldObjectId))
            {
                if (firstById.TryGetValue(info.WorldObjectId, out BreakableInfo first))
                {
                    Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' has duplicate PersistentBreakable worldObjectId '{info.WorldObjectId}' at '{first.ObjectPath}' and '{info.ObjectPath}' — likely a duplicated prefab instance whose ID was not changed.");
                    issues++;
                }
                else
                {
                    firstById.Add(info.WorldObjectId, info);
                }

                if (!needsId)
                {
                    Debug.LogWarning($"[WorldPersistenceValidator] Scene '{sceneName}' PersistentBreakable '{info.ObjectPath}' has lifetime RoomRuntime but a worldObjectId is assigned. RoomRuntime never reads or writes the registry; clear the ID or change the lifetime.");
                }
            }

            if (needsId && !info.HasRegistry)
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' PersistentBreakable '{info.ObjectPath}' has lifetime {info.Lifetime} but no WorldStateRegistry assigned.");
                issues++;
            }

            if (!info.HasSolidCollider)
            {
                Debug.LogError($"[WorldPersistenceValidator] Scene '{sceneName}' PersistentBreakable '{info.ObjectPath}' has no solidCollider assigned — it has no destruction target. Assign the collider that represents this breakable's physical presence.");
                issues++;
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
