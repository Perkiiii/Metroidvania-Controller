using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BossEncounterValidatorTests
{
    [Test]
    public void InvalidParticipantReportsConcreteHierarchyAndPersistenceIssues()
    {
        GameObject wrapper = new GameObject("Invalid Participant");
        GameObject actor = new GameObject("ActorRoot");
        GameObject external = new GameObject("External");
        try
        {
            BossEncounterParticipant participant = wrapper.AddComponent<BossEncounterParticipant>();
            actor.SetActive(true);
            EnemyHealthComponent health = external.AddComponent<EnemyHealthComponent>();
            InvalidBossBehaviour behaviour = external.AddComponent<InvalidBossBehaviour>();
            actor.AddComponent<EnemyPersistence>();
            SetPrivateField(participant, "actorRoot", actor);
            SetPrivateField(participant, "health", health);
            SetPrivateField(participant, "behaviourSource", behaviour);

            LogAssert.ignoreFailingMessages = true;
            int issues = BossEncounterValidator.ValidateParticipant(participant);
            Assert.That(issues, Is.GreaterThanOrEqualTo(4));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
            Object.DestroyImmediate(wrapper);
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(external);
        }
    }

    [Test]
    public void PermanentEnemyIdCollisionIsDetectedAgainstDefinitions()
    {
        GameObject enemy = new GameObject("Legacy Permanent Boss");
        try
        {
            EnemyPersistence persistence = enemy.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.PermanentEncounter);
            SetPrivateField(persistence, "worldObjectId", "shared_boss_id");
            LogAssert.Expect(
                LogType.Error,
                "[BossEncounterValidator] 'Legacy Permanent Boss' PermanentEncounter ID 'shared_boss_id' collides with a BossEncounterDefinition.");

            int issues = BossEncounterValidator.ValidatePermanentEncounterCollision(
                persistence,
                new HashSet<string> { "shared_boss_id" });

            Assert.That(issues, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(enemy);
        }
    }

    [Test]
    public void DuplicateDefinitionAssetIdsRemainDetected()
    {
        BossEncounterDefinition first = CreateDefinition("First", "duplicate_id");
        BossEncounterDefinition second = CreateDefinition("Second", "duplicate_id");
        try
        {
            LogAssert.Expect(
                LogType.Error,
                "[BossEncounterValidator] Encounter ID 'duplicate_id' is duplicated by definitions 'First' and 'Second'.");

            Assert.That(
                BossEncounterValidator.ValidateDefinitions(new[] { first, second }),
                Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void DuplicatePlacementsInSameSceneReportSceneAndHierarchy()
    {
        GameObject firstObject = new GameObject("First Encounter");
        GameObject secondObject = new GameObject("Second Encounter");
        try
        {
            BossEncounterController first = firstObject.AddComponent<BossEncounterController>();
            BossEncounterController second = secondObject.AddComponent<BossEncounterController>();
            Dictionary<string, List<BossEncounterValidator.EncounterPlacement>> placements =
                new Dictionary<string, List<BossEncounterValidator.EncounterPlacement>>
                {
                    ["boss_shared"] = new List<BossEncounterValidator.EncounterPlacement>
                    {
                        new BossEncounterValidator.EncounterPlacement(
                            "Assets/_Project/Scenes/Arena.unity",
                            "Arena/First Encounter",
                            first),
                        new BossEncounterValidator.EncounterPlacement(
                            "Assets/_Project/Scenes/Arena.unity",
                            "Arena/Second Encounter",
                            second)
                    }
                };

            LogAssert.Expect(
                LogType.Error,
                "[BossEncounterValidator] Encounter ID 'boss_shared' is used by multiple BossEncounterController placements: "
                + "'Assets/_Project/Scenes/Arena.unity::Arena/First Encounter', "
                + "'Assets/_Project/Scenes/Arena.unity::Arena/Second Encounter'. "
                + "Each permanent world encounter ID may have only one enabled-build-scene placement.");

            Assert.That(BossEncounterValidator.ValidateEncounterPlacements(placements), Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(secondObject);
        }
    }

    [Test]
    public void DuplicatePlacementsAcrossScenesReportBothScenePaths()
    {
        GameObject firstObject = new GameObject("Encounter");
        GameObject secondObject = new GameObject("Encounter");
        try
        {
            BossEncounterController first = firstObject.AddComponent<BossEncounterController>();
            BossEncounterController second = secondObject.AddComponent<BossEncounterController>();
            Dictionary<string, List<BossEncounterValidator.EncounterPlacement>> placements =
                new Dictionary<string, List<BossEncounterValidator.EncounterPlacement>>
                {
                    ["boss_shared"] = new List<BossEncounterValidator.EncounterPlacement>
                    {
                        new BossEncounterValidator.EncounterPlacement(
                            "Assets/_Project/Scenes/ArenaA.unity",
                            "ArenaA/Encounter",
                            first),
                        new BossEncounterValidator.EncounterPlacement(
                            "Assets/_Project/Scenes/ArenaB.unity",
                            "ArenaB/Encounter",
                            second)
                    }
                };

            LogAssert.Expect(
                LogType.Error,
                "[BossEncounterValidator] Encounter ID 'boss_shared' is used by multiple BossEncounterController placements: "
                + "'Assets/_Project/Scenes/ArenaA.unity::ArenaA/Encounter', "
                + "'Assets/_Project/Scenes/ArenaB.unity::ArenaB/Encounter'. "
                + "Each permanent world encounter ID may have only one enabled-build-scene placement.");

            Assert.That(BossEncounterValidator.ValidateEncounterPlacements(placements), Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(secondObject);
        }
    }

    [Test]
    public void UniquePlacementsAcrossScenesAreClean()
    {
        Dictionary<string, List<BossEncounterValidator.EncounterPlacement>> placements =
            new Dictionary<string, List<BossEncounterValidator.EncounterPlacement>>
            {
                ["boss_first"] = new List<BossEncounterValidator.EncounterPlacement>
                {
                    new BossEncounterValidator.EncounterPlacement(
                        "Assets/_Project/Scenes/ArenaA.unity",
                        "ArenaA/Encounter",
                        null)
                },
                ["boss_second"] = new List<BossEncounterValidator.EncounterPlacement>
                {
                    new BossEncounterValidator.EncounterPlacement(
                        "Assets/_Project/Scenes/ArenaB.unity",
                        "ArenaB/Encounter",
                        null)
                }
            };

        Assert.That(BossEncounterValidator.ValidateEncounterPlacements(placements), Is.Zero);
    }

    [Test]
    public void DisabledSolidBarrierBlockerIsValid()
    {
        GameObject barrierObject = new GameObject("Barrier");
        try
        {
            BoxCollider2D blocker = barrierObject.AddComponent<BoxCollider2D>();
            blocker.enabled = false;
            BossArenaBarrier barrier = barrierObject.AddComponent<BossArenaBarrier>();
            SetPrivateField(barrier, "blockerColliders", new Collider2D[] { blocker });

            Assert.That(BossEncounterValidator.ValidateBarrier(barrier, true), Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(barrierObject);
        }
    }

    [Test]
    public void BarrierReportsNullTriggerReuseAndMissingValidBlocker()
    {
        GameObject barrierObject = new GameObject("Barrier");
        GameObject encounterTriggerObject = new GameObject("Encounter Trigger");
        GameObject cameraTriggerObject = new GameObject("Camera Lock");
        try
        {
            BossArenaBarrier barrier = barrierObject.AddComponent<BossArenaBarrier>();
            BoxCollider2D encounterTrigger = encounterTriggerObject.AddComponent<BoxCollider2D>();
            BoxCollider2D cameraTrigger = cameraTriggerObject.AddComponent<BoxCollider2D>();
            encounterTrigger.isTrigger = true;
            cameraTrigger.isTrigger = true;
            SetPrivateField(
                barrier,
                "blockerColliders",
                new Collider2D[] { null, encounterTrigger, cameraTrigger });

            LogAssert.ignoreFailingMessages = true;
            int issues = BossEncounterValidator.ValidateBarrier(
                barrier,
                true,
                encounterTrigger,
                cameraTrigger);

            Assert.That(issues, Is.EqualTo(6));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
            Object.DestroyImmediate(barrierObject);
            Object.DestroyImmediate(encounterTriggerObject);
            Object.DestroyImmediate(cameraTriggerObject);
        }
    }

    [Test]
    public void BarrierReportsSharedOpenAndClosedPresentationRoot()
    {
        GameObject barrierObject = new GameObject("Barrier");
        GameObject presentation = new GameObject("Presentation");
        try
        {
            presentation.transform.SetParent(barrierObject.transform);
            BossArenaBarrier barrier = barrierObject.AddComponent<BossArenaBarrier>();
            SetPrivateField(barrier, "openPresentationRoot", presentation);
            SetPrivateField(barrier, "closedPresentationRoot", presentation);
            LogAssert.Expect(
                LogType.Error,
                "[BossEncounterValidator] 'Barrier' open and closed presentation roots must not reference the same GameObject.");

            Assert.That(BossEncounterValidator.ValidateBarrier(barrier, false), Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(barrierObject);
        }
    }

    private static BossEncounterDefinition CreateDefinition(string name, string encounterId)
    {
        BossEncounterDefinition definition = ScriptableObject.CreateInstance<BossEncounterDefinition>();
        definition.name = name;
        SetPrivateField(definition, "encounterId", encounterId);
        return definition;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}

public sealed class InvalidBossBehaviour : MonoBehaviour
{
}
