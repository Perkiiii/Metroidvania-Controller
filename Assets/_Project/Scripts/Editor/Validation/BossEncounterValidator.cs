using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BossEncounterValidator
{
    [MenuItem("Tools/Project/Validate Boss Encounters")]
    public static void ValidateBossEncounters()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogWarning("[BossEncounterValidator] Validation cancelled because a loaded scene has unsaved changes. Save or revert it explicitly before validating build scenes.");
                return;
            }
        }

        List<BossEncounterDefinition> definitions = LoadAllDefinitions();
        int issues = ValidateDefinitions(definitions);
        HashSet<string> definitionIds = new HashSet<string>();
        Dictionary<string, List<EncounterPlacement>> placements =
            new Dictionary<string, List<EncounterPlacement>>();
        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] != null && !string.IsNullOrWhiteSpace(definitions[i].EncounterId))
            {
                definitionIds.Add(definitions[i].EncounterId);
            }
        }

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                issues += ValidateScene(scene, definitionIds, placements);
            }

            issues += ValidateEncounterPlacements(placements);
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }

        if (issues == 0)
        {
            Debug.Log("[BossEncounterValidator] Boss encounter foundation validation passed.");
        }
        else
        {
            Debug.LogWarning($"[BossEncounterValidator] Found {issues} issue(s). See earlier logs.");
        }
    }

    internal static int ValidateDefinitions(IReadOnlyList<BossEncounterDefinition> definitions)
    {
        int issues = 0;
        Dictionary<string, BossEncounterDefinition> byId = new Dictionary<string, BossEncounterDefinition>();

        for (int i = 0; i < definitions.Count; i++)
        {
            BossEncounterDefinition definition = definitions[i];
            if (definition == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(definition.EncounterId))
            {
                Debug.LogError($"[BossEncounterValidator] Definition '{definition.name}' has an empty encounter ID.", definition);
                issues++;
                continue;
            }

            if (byId.TryGetValue(definition.EncounterId, out BossEncounterDefinition existing))
            {
                Debug.LogError($"[BossEncounterValidator] Encounter ID '{definition.EncounterId}' is duplicated by definitions '{existing.name}' and '{definition.name}'.", definition);
                issues++;
            }
            else
            {
                byId.Add(definition.EncounterId, definition);
            }
        }

        return issues;
    }

    internal static int ValidateController(BossEncounterController controller)
    {
        int issues = 0;
        if (controller.Definition == null)
        {
            issues += Error(controller, "is missing BossEncounterDefinition.");
        }
        else if (string.IsNullOrWhiteSpace(controller.Definition.EncounterId))
        {
            issues += Error(controller, "references a definition with an empty encounter ID.");
        }

        if (controller.WorldStateRegistry == null)
        {
            issues += Error(controller, "is missing WorldStateRegistry.");
        }

        BossEncounterParticipant[] participants = controller.Participants;
        if (participants == null || participants.Length == 0)
        {
            issues += Error(controller, "has no participants.");
        }
        else
        {
            HashSet<BossEncounterParticipant> unique = new HashSet<BossEncounterParticipant>();
            for (int i = 0; i < participants.Length; i++)
            {
                BossEncounterParticipant participant = participants[i];
                if (participant == null)
                {
                    issues += Error(controller, $"has a null participant at index {i}.");
                    continue;
                }

                if (!unique.Add(participant))
                {
                    issues += Error(participant, "is listed more than once by the same encounter.");
                    continue;
                }

                issues += ValidateParticipant(participant);
            }
        }

        if (controller.RequiresTrigger)
        {
            if (controller.Trigger == null)
            {
                issues += Error(controller, "requires a start trigger but none is assigned.");
            }
            else if (controller.Trigger.Encounter != controller)
            {
                issues += Error(controller.Trigger, "does not reference its owning encounter controller.");
            }
        }

        if (controller.RequiresBarriers && (controller.Barriers == null || controller.Barriers.Length == 0))
        {
            issues += Error(controller, "requires barriers but none are assigned.");
        }
        else if (controller.Barriers != null)
        {
            HashSet<BossArenaBarrier> uniqueBarriers = new HashSet<BossArenaBarrier>();
            for (int i = 0; i < controller.Barriers.Length; i++)
            {
                BossArenaBarrier barrier = controller.Barriers[i];
                if (barrier == null)
                {
                    issues += Error(controller, $"has a null barrier at index {i}.");
                }
                else if (!uniqueBarriers.Add(barrier))
                {
                    issues += Error(barrier, "is listed more than once by the same encounter.");
                }
                else
                {
                    issues += ValidateBarrier(
                        barrier,
                        controller.RequiresBarriers,
                        controller.Trigger != null ? controller.Trigger.GetComponent<Collider2D>() : null,
                        controller.CameraLockArea != null
                            ? controller.CameraLockArea.GetComponent<Collider2D>()
                            : null);
                }
            }
        }

        if (controller.RequiresCameraLock && controller.CameraLockArea == null)
        {
            issues += Error(controller, "requires a CameraLockArea but none is assigned.");
        }

        if (controller.RewardRoot != null)
        {
            if (controller.RewardRoot == controller.gameObject)
            {
                issues += Error(controller, "cannot use its own GameObject as RewardRoot.");
            }
            else if (controller.transform.IsChildOf(controller.RewardRoot.transform))
            {
                issues += Error(controller, "cannot be parented beneath RewardRoot because hiding the reward would disable the encounter.");
            }
        }

        return issues;
    }

    internal static int ValidateParticipant(BossEncounterParticipant participant)
    {
        int issues = 0;
        if (!participant.gameObject.activeSelf)
        {
            issues += Error(participant, "participant wrapper must be authored active.");
        }

        GameObject actorRoot = participant.ActorRoot;
        if (actorRoot == null)
        {
            return issues + Error(participant, "is missing ActorRoot.");
        }

        if (actorRoot.activeSelf)
        {
            issues += Error(participant, "ActorRoot must be authored inactive.");
        }

        if (!actorRoot.transform.IsChildOf(participant.transform))
        {
            issues += Error(participant, "ActorRoot must be a child of its active participant wrapper.");
        }

        if (participant.Health == null)
        {
            issues += Error(participant, "is missing its actor health reference.");
        }
        else if (!participant.Health.transform.IsChildOf(actorRoot.transform)
            && participant.Health.gameObject != actorRoot)
        {
            issues += Error(participant, "health reference must belong to ActorRoot.");
        }

        if (participant.BehaviourSource == null)
        {
            issues += Error(participant, "is missing its behavior reference.");
        }
        else if (!participant.HasValidBehaviour)
        {
            issues += Error(participant, "behavior source does not implement IBossEncounterBehaviour.");
        }
        else if (!participant.BehaviourSource.transform.IsChildOf(actorRoot.transform)
            && participant.BehaviourSource.gameObject != actorRoot)
        {
            issues += Error(participant, "behavior reference must belong to ActorRoot.");
        }

        EnemyPersistence persistence = actorRoot.GetComponentInChildren<EnemyPersistence>(true);
        if (persistence != null)
        {
            issues += Error(persistence, "coordinated encounter actors must not carry EnemyPersistence.");
        }

        return issues;
    }

    internal static int ValidateBarrier(
        BossArenaBarrier barrier,
        bool requireValidBlocker,
        Collider2D encounterTriggerCollider = null,
        Collider2D cameraLockCollider = null)
    {
        if (barrier == null)
        {
            return 0;
        }

        int issues = 0;
        int validBlockers = 0;
        Collider2D[] blockers = barrier.BlockerColliders;
        for (int i = 0; blockers != null && i < blockers.Length; i++)
        {
            Collider2D blocker = blockers[i];
            if (blocker == null)
            {
                issues += Error(barrier, $"has a null blocker collider at index {i}.");
                continue;
            }

            bool valid = true;
            if (blocker.isTrigger)
            {
                issues += Error(blocker, "blocker collider must be non-trigger so it can block the arena when enabled.");
                valid = false;
            }

            if (ReferenceEquals(blocker, encounterTriggerCollider))
            {
                issues += Error(blocker, "must not reuse the encounter trigger collider as a barrier blocker.");
                valid = false;
            }

            if (ReferenceEquals(blocker, cameraLockCollider))
            {
                issues += Error(blocker, "must not reuse the camera-lock trigger collider as a barrier blocker.");
                valid = false;
            }

            if (valid)
            {
                validBlockers++;
            }
        }

        if (requireValidBlocker && validBlockers == 0)
        {
            issues += Error(barrier, "requires at least one non-trigger blocker collider suitable for solid blocking when enabled.");
        }

        if (barrier.OpenPresentationRoot != null
            && ReferenceEquals(barrier.OpenPresentationRoot, barrier.ClosedPresentationRoot))
        {
            issues += Error(barrier, "open and closed presentation roots must not reference the same GameObject.");
        }

        return issues;
    }

    private static int ValidateScene(
        Scene scene,
        HashSet<string> definitionIds,
        Dictionary<string, List<EncounterPlacement>> placements)
    {
        int issues = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (EnemyPersistence persistence in root.GetComponentsInChildren<EnemyPersistence>(true))
            {
                if (persistence.Mode == EnemyPersistenceMode.PermanentEncounter
                    && !string.IsNullOrWhiteSpace(persistence.WorldObjectId))
                {
                    issues += ValidatePermanentEncounterCollision(persistence, definitionIds);
                }
            }
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (BossEncounterController controller in root.GetComponentsInChildren<BossEncounterController>(true))
            {
                issues += ValidateController(controller);
            }
        }

        CollectEncounterPlacements(scene, placements);

        return issues;
    }

    internal static void CollectEncounterPlacements(
        Scene scene,
        Dictionary<string, List<EncounterPlacement>> placements)
    {
        if (!scene.IsValid() || placements == null)
        {
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (BossEncounterController controller in root.GetComponentsInChildren<BossEncounterController>(true))
            {
                string id = controller.Definition != null ? controller.Definition.EncounterId : "";
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (!placements.TryGetValue(id, out List<EncounterPlacement> byId))
                {
                    byId = new List<EncounterPlacement>();
                    placements.Add(id, byId);
                }

                byId.Add(new EncounterPlacement(scene.path, GetPath(controller.transform), controller));
            }
        }
    }

    internal static int ValidateEncounterPlacements(
        IReadOnlyDictionary<string, List<EncounterPlacement>> placements)
    {
        if (placements == null)
        {
            return 0;
        }

        int issues = 0;
        foreach (KeyValuePair<string, List<EncounterPlacement>> pair in placements)
        {
            List<EncounterPlacement> byId = pair.Value;
            if (byId == null || byId.Count < 2)
            {
                continue;
            }

            List<string> locations = new List<string>(byId.Count);
            for (int i = 0; i < byId.Count; i++)
            {
                locations.Add($"'{byId[i].ScenePath}::{byId[i].HierarchyPath}'");
            }

            Debug.LogError(
                $"[BossEncounterValidator] Encounter ID '{pair.Key}' is used by multiple BossEncounterController placements: "
                + string.Join(", ", locations)
                + ". Each permanent world encounter ID may have only one enabled-build-scene placement.",
                byId[byId.Count - 1].Controller);
            issues++;
        }

        return issues;
    }

    internal static int ValidatePermanentEncounterCollision(
        EnemyPersistence persistence,
        ISet<string> encounterDefinitionIds)
    {
        if (persistence == null
            || persistence.Mode != EnemyPersistenceMode.PermanentEncounter
            || string.IsNullOrWhiteSpace(persistence.WorldObjectId)
            || encounterDefinitionIds == null
            || !encounterDefinitionIds.Contains(persistence.WorldObjectId))
        {
            return 0;
        }

        return Error(
            persistence,
            $"PermanentEncounter ID '{persistence.WorldObjectId}' collides with a BossEncounterDefinition.");
    }

    private static List<BossEncounterDefinition> LoadAllDefinitions()
    {
        string[] guids = AssetDatabase.FindAssets("t:BossEncounterDefinition");
        List<BossEncounterDefinition> definitions = new List<BossEncounterDefinition>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            BossEncounterDefinition definition = AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>(
                AssetDatabase.GUIDToAssetPath(guids[i]));
            if (definition != null)
            {
                definitions.Add(definition);
            }
        }

        return definitions;
    }

    private static int Error(Object context, string message)
    {
        Debug.LogError($"[BossEncounterValidator] '{GetPath(context is Component component ? component.transform : null)}' {message}", context);
        return 1;
    }

    private static string GetPath(Transform transform)
    {
        if (transform == null)
        {
            return "<asset>";
        }

        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }

    internal readonly struct EncounterPlacement
    {
        public EncounterPlacement(string scenePath, string hierarchyPath, BossEncounterController controller)
        {
            ScenePath = scenePath;
            HierarchyPath = hierarchyPath;
            Controller = controller;
        }

        public string ScenePath { get; }
        public string HierarchyPath { get; }
        public BossEncounterController Controller { get; }
    }
}
