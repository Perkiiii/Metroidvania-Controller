using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Focused hardening pass for World Persistence Phase 3: bulk correctness/determinism, repeated
// gather/apply idempotence, slot-switching isolation, and subscription cleanup. Existing
// WorldStateRegistryTests already cover round-tripping, dedup notifications, and single-target
// save independence at small scale -- this file extends those same guarantees to scale and to
// repeated-operation scenarios, without introducing any new performance infrastructure (plain
// Dictionary/HashSet usage only, matching production code).
public sealed class WorldPersistenceHardeningTests
{
    private const int BulkCount = 5000;

    [Test]
    public void ThousandsOfPermanentObjectStates_RoundTripDeterministicallySorted()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            for (int i = 0; i < BulkCount; i++)
                registry.SetObjectState($"door_{i:D5}", i % 2 == 0 ? "open" : "closed");

            SaveData first = new SaveData();
            registry.GatherSaveData(first);

            SaveData second = new SaveData();
            registry.GatherSaveData(second);

            Assert.That(first.world.objectStates.Count, Is.EqualTo(BulkCount));
            Assert.That(second.world.objectStates.Count, Is.EqualTo(BulkCount));

            // Gathering repeatedly from unchanged state must produce byte-for-byte identical
            // ordering -- save-target order and repeated saves must never affect output.
            for (int i = 0; i < BulkCount; i++)
            {
                Assert.That(second.world.objectStates[i].id, Is.EqualTo(first.world.objectStates[i].id));
                Assert.That(second.world.objectStates[i].state, Is.EqualTo(first.world.objectStates[i].state));
            }

            // Sorted ordinal by id.
            for (int i = 1; i < first.world.objectStates.Count; i++)
            {
                Assert.That(
                    string.CompareOrdinal(first.world.objectStates[i - 1].id, first.world.objectStates[i].id),
                    Is.LessThan(0),
                    "Gathered permanent object states must be sorted deterministically by id.");
            }
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void ThousandsOfTransientRecords_AreClearedTogetherAndNeverSerialized()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            for (int i = 0; i < BulkCount; i++)
            {
                registry.RecordRespawnableEnemyDeath($"enemy_{i:D5}", 9999f);
                registry.SetUntilDeathState($"lever_{i:D5}", "raised");
            }

            SaveData data = new SaveData();
            registry.GatherSaveData(data);
            Assert.That(data.world.objectStates, Is.Empty, "Until-death state must never serialize, even at scale.");

            registry.ResetRespawnableEnemyDeaths();
            registry.ResetUntilDeathState();

            Assert.That(InvokeShouldSuppress(registry, "enemy_00000"), Is.False);
            Assert.That(InvokeShouldSuppress(registry, $"enemy_{BulkCount - 1:D5}"), Is.False);
            Assert.That(registry.TryGetUntilDeathState("lever_00000", out _), Is.False);
            Assert.That(registry.TryGetUntilDeathState($"lever_{BulkCount - 1:D5}", out _), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void RepeatedGatherAndApply_IsIdempotentAndStable()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            registry.MarkRoomVisited("SampleScene");
            registry.MarkPickupCollected("pickup_a");
            registry.MarkEncounterDefeated("boss_a");
            registry.SetObjectState("door_a", "open");

            SaveData data = new SaveData();
            for (int i = 0; i < 20; i++)
                registry.GatherSaveData(data);

            Assert.That(data.world.visitedRoomIds, Has.Exactly(1).EqualTo("SampleScene"));
            Assert.That(data.world.collectedPickupIds, Has.Exactly(1).EqualTo("pickup_a"));
            Assert.That(data.world.defeatedEncounterIds, Has.Exactly(1).EqualTo("boss_a"));
            Assert.That(data.world.objectStates, Has.Exactly(1).Matches<WorldObjectStateEntry>(e => e.id == "door_a" && e.state == "open"));

            for (int i = 0; i < 20; i++)
                registry.ApplySaveData(data);

            Assert.That(registry.IsRoomVisited("SampleScene"), Is.True);
            Assert.That(registry.IsPickupCollected("pickup_a"), Is.True);
            Assert.That(registry.IsEncounterDefeated("boss_a"), Is.True);
            Assert.That(registry.TryGetObjectState("door_a", out string state), Is.True);
            Assert.That(state, Is.EqualTo("open"));
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void SlotSwitch_ThenFreshGame_LoadsOnlySelectedSlotStateThenClearsCompletely()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            SaveData slotA = new SaveData();
            slotA.world.visitedRoomIds.Add("SampleScene");
            slotA.world.collectedPickupIds.Add("pickup_slotA");
            slotA.world.objectStates.Add(new WorldObjectStateEntry { id = "door_slotA", state = "open" });

            SaveData slotB = new SaveData();
            slotB.world.visitedRoomIds.Add("SampleScene2");
            slotB.world.defeatedEncounterIds.Add("boss_slotB");

            registry.ApplySaveData(slotA);
            Assert.That(registry.IsRoomVisited("SampleScene"), Is.True);
            Assert.That(registry.IsPickupCollected("pickup_slotA"), Is.True);

            // Switching slots must not merge -- slot B must fully replace slot A's state.
            registry.ApplySaveData(slotB);
            Assert.That(registry.IsRoomVisited("SampleScene"), Is.False, "Slot switch must not leak the previous slot's visited rooms.");
            Assert.That(registry.IsPickupCollected("pickup_slotA"), Is.False, "Slot switch must not leak the previous slot's pickups.");
            Assert.That(registry.TryGetObjectState("door_slotA", out _), Is.False, "Slot switch must not leak the previous slot's object states.");
            Assert.That(registry.IsRoomVisited("SampleScene2"), Is.True);
            Assert.That(registry.IsEncounterDefeated("boss_slotB"), Is.True);

            // Fresh game (New Game) applies an empty SaveData -- must clear every collection.
            registry.ApplySaveData(new SaveData());
            Assert.That(registry.IsRoomVisited("SampleScene2"), Is.False);
            Assert.That(registry.IsEncounterDefeated("boss_slotB"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Unsubscribe_StopsReceivingNotifications_AndDoesNotAffectOtherSubscribers()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            WorldStateKey key = new WorldStateKey(WorldStateCategory.ObjectState, "door_unsub");
            int callbackACount = 0;
            int callbackBCount = 0;
            System.Action<WorldStateChange> callbackA = _ => callbackACount++;
            System.Action<WorldStateChange> callbackB = _ => callbackBCount++;

            registry.Subscribe(key, callbackA);
            registry.Subscribe(key, callbackB);

            registry.SetObjectState("door_unsub", "open");
            Assert.That(callbackACount, Is.EqualTo(1));
            Assert.That(callbackBCount, Is.EqualTo(1));

            registry.Unsubscribe(key, callbackA);
            registry.SetObjectState("door_unsub", "closed");

            Assert.That(callbackACount, Is.EqualTo(1), "Unsubscribed callback must not receive further notifications.");
            Assert.That(callbackBCount, Is.EqualTo(2), "Unsubscribing one callback must not affect other subscribers of the same key.");
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Unsubscribe_UnknownCallback_DoesNotThrow()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            WorldStateKey key = new WorldStateKey(WorldStateCategory.ObjectState, "door_never_subscribed");
            System.Action<WorldStateChange> callback = _ => { };

            Assert.DoesNotThrow(() => registry.Unsubscribe(key, callback));
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void GatherSaveData_NeverMutatesUnrelatedSaveDomains()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            registry.SetObjectState("door_isolated", "open");

            SaveData data = new SaveData();
            data.abilities.dashUnlocked = false;
            data.health.currentHealth = 7;
            data.resource.currentParts = 3;
            data.player.activeRespawnMarkerKey = "checkpoint_x";

            registry.GatherSaveData(data);

            Assert.That(data.abilities.dashUnlocked, Is.False, "GatherSaveData must not touch the abilities section.");
            Assert.That(data.health.currentHealth, Is.EqualTo(7), "GatherSaveData must not touch the health section.");
            Assert.That(data.resource.currentParts, Is.EqualTo(3), "GatherSaveData must not touch the resource section.");
            Assert.That(data.player.activeRespawnMarkerKey, Is.EqualTo("checkpoint_x"), "GatherSaveData must not touch the player section.");
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    private static bool InvokeShouldSuppress(WorldStateRegistry registry, string enemyId)
    {
        System.Reflection.MethodInfo method = typeof(WorldStateRegistry).GetMethod(
            "ShouldSuppressEnemyOnInitialization",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return (bool)method.Invoke(registry, new object[] { enemyId });
    }
}
