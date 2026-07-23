using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class GameManagerWorldPersistenceLifecycleTests
{
    // ApplyNormalDeathRespawn is the single convergence point for both same-scene and
    // cross-scene normal death (see BeginRespawnSequence / CompletePendingNormalDeathRespawn),
    // and is never reached by checkpoint activation or recoverable hazard reposition -- both of
    // those paths (CheckpointInteractable.Interact, HeroBox.HandleHazard,
    // GameManager.BeginHazardRecoverySequence, HeroController.ResetAfterHazardRecovery) never
    // reference WorldStateRegistry at all, so no separate regression test is needed for them.

    [Test]
    public void ApplyNormalDeathRespawn_ClearsTimedEnemyDeathsAndUntilDeathState()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("GameManager Test");
        try
        {
            registry.RecordRespawnableEnemyDeath("enemy_1", 9999f);
            registry.SetUntilDeathState("lever_1", "raised");

            go.SetActive(false); // prevent Unity's own Awake pass; we invoke the target method directly
            GameManager gm = go.AddComponent<GameManager>();
            SetPrivateField(gm, "worldStateRegistry", registry);

            InvokePrivate(gm, "ApplyNormalDeathRespawn", null, "TestScene");

            Assert.That(InvokeShouldSuppress(registry, "enemy_1"), Is.False,
                "Normal death must clear timed enemy death records.");
            Assert.That(registry.TryGetUntilDeathState("lever_1", out _), Is.False,
                "Normal death must clear generic until-death state.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void ApplyNormalDeathRespawn_PreservesPermanentWorldState()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("GameManager Test");
        try
        {
            registry.MarkEncounterDefeated("boss_1");
            registry.MarkPickupCollected("pickup_1");
            registry.MarkRoomVisited("room_1");
            registry.SetObjectState("door_1", "open");

            go.SetActive(false);
            GameManager gm = go.AddComponent<GameManager>();
            SetPrivateField(gm, "worldStateRegistry", registry);

            InvokePrivate(gm, "ApplyNormalDeathRespawn", null, "TestScene");

            Assert.That(registry.IsEncounterDefeated("boss_1"), Is.True, "Normal death must not clear permanent encounter completion.");
            Assert.That(registry.IsPickupCollected("pickup_1"), Is.True, "Normal death must not clear collected pickups.");
            Assert.That(registry.IsRoomVisited("room_1"), Is.True, "Normal death must not clear visited rooms.");
            Assert.That(registry.TryGetObjectState("door_1", out string state), Is.True, "Normal death must not clear permanent object state.");
            Assert.That(state, Is.EqualTo("open"));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void ApplyNormalDeathRespawn_MissingRegistry_DoesNotThrow()
    {
        GameObject go = new GameObject("GameManager Test");
        try
        {
            go.SetActive(false);
            GameManager gm = go.AddComponent<GameManager>();

            Assert.DoesNotThrow(() => InvokePrivate(gm, "ApplyNormalDeathRespawn", null, "TestScene"));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static bool InvokeShouldSuppress(WorldStateRegistry registry, string enemyId)
    {
        MethodInfo method = typeof(WorldStateRegistry).GetMethod("ShouldSuppressEnemyOnInitialization", BindingFlags.Instance | BindingFlags.NonPublic);
        return (bool)method.Invoke(registry, new object[] { enemyId });
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on '{target.GetType().Name}'.");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Method '{methodName}' not found on '{target.GetType().Name}'.");
        method.Invoke(target, arguments);
    }
}
