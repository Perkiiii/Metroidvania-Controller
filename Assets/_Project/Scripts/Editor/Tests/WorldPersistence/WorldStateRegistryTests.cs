using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class WorldStateRegistryTests
{
    [Test]
    public void MarkRoomVisited_RoundTripsThroughSave_AndDedupesNotifications()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            int notifyCount = 0;
            registry.Subscribe(new WorldStateKey(WorldStateCategory.VisitedRoom, "room_a"), _ => notifyCount++);

            Assert.That(registry.IsRoomVisited("room_a"), Is.False);

            registry.MarkRoomVisited("room_a");
            registry.MarkRoomVisited("room_a"); // second call must not re-notify

            Assert.That(registry.IsRoomVisited("room_a"), Is.True);
            Assert.That(notifyCount, Is.EqualTo(1));

            SaveData data = new SaveData();
            registry.GatherSaveData(data);
            Assert.That(data.world.visitedRoomIds, Contains.Item("room_a"));

            WorldStateRegistry destination = ScriptableObject.CreateInstance<WorldStateRegistry>();
            try
            {
                destination.ApplySaveData(data);
                Assert.That(destination.IsRoomVisited("room_a"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(destination);
            }
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void MarkPickupCollected_EmptyId_LogsWarningAndIsIgnored()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            LogAssert.Expect(LogType.Warning, "[WorldStateRegistry] MarkPickupCollected called with an empty pickupId; ignoring.");
            registry.MarkPickupCollected("");

            Assert.That(registry.IsPickupCollected(""), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void MarkEncounterDefeated_PersistsAcrossSaveRoundTrip()
    {
        WorldStateRegistry source = ScriptableObject.CreateInstance<WorldStateRegistry>();
        WorldStateRegistry destination = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            source.MarkEncounterDefeated("boss_1");
            SaveData data = new SaveData();
            source.GatherSaveData(data);
            destination.ApplySaveData(data);

            Assert.That(destination.IsEncounterDefeated("boss_1"), Is.True);
            Assert.That(data.world.defeatedEncounterIds, Contains.Item("boss_1"));
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(destination);
        }
    }

    [Test]
    public void ClearEncounterDefeatedRecord_RemovesRecordWithoutNotifying()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            registry.MarkEncounterDefeated("boss_1");
            Assert.That(registry.IsEncounterDefeated("boss_1"), Is.True);

            int notifyCount = 0;
            registry.Subscribe(new WorldStateKey(WorldStateCategory.DefeatedEncounter, "boss_1"), _ => notifyCount++);

            bool removed = registry.EditorClearEncounterDefeatedRecord("boss_1");

            Assert.That(removed, Is.True);
            Assert.That(registry.IsEncounterDefeated("boss_1"), Is.False);
            Assert.That(notifyCount, Is.EqualTo(0), "Clearing a defeated record is debug-only and must never notify subscribers.");
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void ClearEncounterDefeatedRecord_EmptyOrUnknownId_IsNoOp()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            registry.MarkEncounterDefeated("boss_1");

            Assert.That(registry.EditorClearEncounterDefeatedRecord(""), Is.False);
            Assert.That(registry.EditorClearEncounterDefeatedRecord("boss_unknown"), Is.False);

            Assert.That(registry.IsEncounterDefeated("boss_1"), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void ObjectState_RoundTripsThroughSave_AndOverwriteNotifiesWithNewValue()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            WorldStateChange? lastChange = null;
            registry.Subscribe(new WorldStateKey(WorldStateCategory.ObjectState, "door_1"), c => lastChange = c);

            registry.SetObjectState("door_1", "open");
            Assert.That(lastChange?.StateValue, Is.EqualTo("open"));

            registry.SetObjectState("door_1", "open"); // no change -> no re-notify
            Assert.That(registry.TryGetObjectState("door_1", out string state), Is.True);
            Assert.That(state, Is.EqualTo("open"));

            SaveData data = new SaveData();
            registry.GatherSaveData(data);

            WorldStateRegistry destination = ScriptableObject.CreateInstance<WorldStateRegistry>();
            try
            {
                destination.ApplySaveData(data);
                Assert.That(destination.TryGetObjectState("door_1", out string loaded), Is.True);
                Assert.That(loaded, Is.EqualTo("open"));
            }
            finally
            {
                Object.DestroyImmediate(destination);
            }
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void UntilDeathState_IsNeverSerializedAndClearsOnApplyOrReset()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            registry.SetUntilDeathState("lever_1", "raised");
            Assert.That(registry.TryGetUntilDeathState("lever_1", out _), Is.True);

            SaveData data = new SaveData();
            registry.GatherSaveData(data);
            Assert.That(data.world.objectStates, Is.Empty, "Until-death state must never be written to WorldSaveData.");

            registry.ResetUntilDeathState();
            Assert.That(registry.TryGetUntilDeathState("lever_1", out _), Is.False);

            registry.SetUntilDeathState("lever_2", "raised");
            registry.ApplySaveData(new SaveData());
            Assert.That(registry.TryGetUntilDeathState("lever_2", out _), Is.False, "ApplySaveData must clear until-death state (fresh game / Continue / slot change).");
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void RespawnableEnemyDeath_ValidRecordSuppressesUntilExpiredOrReset()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            Assert.That(InvokeShouldSuppress(registry, "mushroom_1"), Is.False, "No record yet -- must not suppress.");

            registry.RecordRespawnableEnemyDeath("mushroom_1", 9999f);
            Assert.That(InvokeShouldSuppress(registry, "mushroom_1"), Is.True, "Fresh long-duration record must suppress.");

            registry.ResetRespawnableEnemyDeaths();
            Assert.That(InvokeShouldSuppress(registry, "mushroom_1"), Is.False, "ResetRespawnableEnemyDeaths must clear all records.");
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void RespawnableEnemyDeath_ZeroDurationRecordIsAlreadyExpiredOnFirstQuery()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            registry.RecordRespawnableEnemyDeath("mushroom_2", 0f);
            Assert.That(InvokeShouldSuppress(registry, "mushroom_2"), Is.False);
            // Expired records are removed as a side effect of the query, never re-suppressing.
            Assert.That(InvokeShouldSuppress(registry, "mushroom_2"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void RespawnableEnemyDeath_IsNotSerializedAndClearsOnApply()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            registry.RecordRespawnableEnemyDeath("mushroom_3", 9999f);

            SaveData data = new SaveData();
            registry.GatherSaveData(data);
            // WorldSaveData has no field for enemy death records at all -- nothing to assert on
            // data directly, but ApplySaveData (fresh game / Continue / slot change) must clear it.
            registry.ApplySaveData(data);

            Assert.That(InvokeShouldSuppress(registry, "mushroom_3"), Is.False, "ApplySaveData must clear respawnable-enemy death records.");
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void SaveTargetIsIndependentOfOtherSections()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            registry.MarkRoomVisited("room_x");
            registry.MarkPickupCollected("pickup_x");

            SaveData data = new SaveData();
            data.health.currentHealth = 3; // simulate another target's section already populated
            registry.GatherSaveData(data);

            Assert.That(data.health.currentHealth, Is.EqualTo(3), "GatherSaveData must not touch sections owned by other save targets.");
            Assert.That(data.world.visitedRoomIds, Contains.Item("room_x"));
            Assert.That(data.world.collectedPickupIds, Contains.Item("pickup_x"));
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void VersionThreeSaveMigratesToVersionSixWithEmptyWorldPersistenceSections()
    {
        const string json = "{\"meta\":{\"saveVersion\":3},\"abilities\":{},\"player\":{},\"health\":{\"initialized\":true},\"resource\":{\"initialized\":true},\"world\":{\"collectedPickupIds\":[\"old_pickup\"]}}";
        Assert.That(SaveSerializer.TryDeserialize(json, out SaveData data), Is.True);

        SaveDataMigrator.Migrate(data);

        Assert.That(data.meta.saveVersion, Is.EqualTo(6));
        Assert.That(data.world.visitedRoomIds, Is.Not.Null);
        Assert.That(data.world.visitedRoomIds, Is.Empty);
        Assert.That(data.world.defeatedEncounterIds, Is.Not.Null);
        Assert.That(data.world.defeatedEncounterIds, Is.Empty);
        Assert.That(data.world.objectStates, Is.Not.Null);
        Assert.That(data.world.objectStates, Is.Empty);
        Assert.That(data.world.collectedPickupIds, Contains.Item("old_pickup"), "Existing pickup IDs must be retained across the version bump.");
    }

    private static bool InvokeShouldSuppress(WorldStateRegistry registry, string enemyId)
    {
        MethodInfo method = typeof(WorldStateRegistry).GetMethod("ShouldSuppressEnemyOnInitialization", BindingFlags.Instance | BindingFlags.NonPublic);
        return (bool)method.Invoke(registry, new object[] { enemyId });
    }

}
