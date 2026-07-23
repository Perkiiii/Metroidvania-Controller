using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PersistentBreakableTests
{
    [Test]
    public void Awake_RoomRuntimeLifetime_AlwaysStartsIntact_ResettingOnEveryRecreation()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject firstGo = new GameObject("Breakable");
        GameObject secondGo = new GameObject("Breakable");
        try
        {
            PersistentBreakable first = SetUpBreakable(firstGo, "breakable_1", registry, PersistenceLifetime.RoomRuntime);
            InvokePrivate(first, "Awake");

            HeroAttackHit hit = new HeroAttackHit(null, HeroAttackDirection.Side, 5, Vector2.zero, Vector2.zero);
            first.ReceiveHeroAttack(hit);

            Assert.That(registry.TryGetObjectState("breakable_1", out _), Is.False, "RoomRuntime must never write permanent state.");
            Assert.That(registry.TryGetUntilDeathState("breakable_1", out _), Is.False, "RoomRuntime must never write until-death state.");

            // A "scene recreation" is simulated by a fresh instance with the same id against the
            // same registry -- since RoomRuntime never wrote anything, it must initialize intact.
            PersistentBreakable second = SetUpBreakable(secondGo, "breakable_1", registry, PersistenceLifetime.RoomRuntime);
            Collider2D secondCollider = (Collider2D)GetPrivateField(second, "solidCollider");
            InvokePrivate(second, "Awake");

            Assert.That(secondCollider.enabled, Is.True, "RoomRuntime breakables always reset to the authored default (intact) on scene reload.");
        }
        finally
        {
            Object.DestroyImmediate(firstGo);
            Object.DestroyImmediate(secondGo);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void UntilDeath_DestroyedState_PersistsAcrossSimulatedRoomReinitialization()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject firstGo = new GameObject("Breakable");
        GameObject secondGo = new GameObject("Breakable");
        try
        {
            PersistentBreakable first = SetUpBreakable(firstGo, "breakable_2", registry, PersistenceLifetime.UntilDeath);
            InvokePrivate(first, "Awake");

            HeroAttackHit hit = new HeroAttackHit(null, HeroAttackDirection.Side, 5, Vector2.zero, Vector2.zero);
            first.ReceiveHeroAttack(hit);

            Assert.That(registry.TryGetUntilDeathState("breakable_2", out string state), Is.True);
            Assert.That(state, Is.EqualTo("destroyed"));

            // Simulate re-entering the room: a fresh EnemyController-equivalent initialization
            // against the same still-populated until-death state (room transitions never clear it).
            PersistentBreakable second = SetUpBreakable(secondGo, "breakable_2", registry, PersistenceLifetime.UntilDeath);
            Collider2D secondCollider = (Collider2D)GetPrivateField(second, "solidCollider");
            InvokePrivate(second, "Awake");

            Assert.That(secondCollider.enabled, Is.False, "UntilDeath destroyed state must persist across room transitions.");
        }
        finally
        {
            Object.DestroyImmediate(firstGo);
            Object.DestroyImmediate(secondGo);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void UntilDeath_DestroyedState_ResetsOnNormalDeath()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Breakable");
        try
        {
            registry.SetUntilDeathState("breakable_3", "destroyed");

            // Exactly the reset GameManager.BeginRespawnSequence performs on every normal death.
            registry.ResetUntilDeathState();

            PersistentBreakable breakable = SetUpBreakable(go, "breakable_3", registry, PersistenceLifetime.UntilDeath);
            Collider2D collider = (Collider2D)GetPrivateField(breakable, "solidCollider");
            InvokePrivate(breakable, "Awake");

            Assert.That(collider.enabled, Is.True, "Normal death must clear UntilDeath breakable state before the checkpoint scene initializes.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Permanent_DestroyedState_SurvivesNormalDeathResetAndSaveRoundTrip()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Breakable");
        try
        {
            PersistentBreakable breakable = SetUpBreakable(go, "breakable_4", registry, PersistenceLifetime.Permanent);
            InvokePrivate(breakable, "Awake");

            HeroAttackHit hit = new HeroAttackHit(null, HeroAttackDirection.Side, 5, Vector2.zero, Vector2.zero);
            breakable.ReceiveHeroAttack(hit);

            // Survives the exact resets a normal death performs.
            registry.ResetRespawnableEnemyDeaths();
            registry.ResetUntilDeathState();
            Assert.That(registry.TryGetObjectState("breakable_4", out string state), Is.True);
            Assert.That(state, Is.EqualTo("destroyed"));

            // Survives a save round-trip (Continue).
            SaveData data = new SaveData();
            registry.GatherSaveData(data);
            WorldStateRegistry destination = ScriptableObject.CreateInstance<WorldStateRegistry>();
            try
            {
                destination.ApplySaveData(data);
                Assert.That(destination.TryGetObjectState("breakable_4", out string loaded), Is.True);
                Assert.That(loaded, Is.EqualTo("destroyed"));
            }
            finally
            {
                Object.DestroyImmediate(destination);
            }
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Awake_StoredDestroyed_DisablesCollisionAndVisualsWithoutReplayingFeedback()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Breakable");
        try
        {
            registry.SetObjectState("breakable_5", "destroyed");
            PersistentBreakable breakable = SetUpBreakable(go, "breakable_5", registry, PersistenceLifetime.Permanent);
            Collider2D collider = (Collider2D)GetPrivateField(breakable, "solidCollider");
            GameObject intactVisual = (GameObject)GetPrivateField(breakable, "intactVisualRoot");

            Assert.DoesNotThrow(() => InvokePrivate(breakable, "Awake"));
            LogAssert.NoUnexpectedReceived();

            Assert.That(collider.enabled, Is.False, "Restored destroyed state must disable collision immediately.");
            Assert.That(intactVisual.activeSelf, Is.False, "Restored destroyed state must hide the intact visual.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void ReceiveHeroAttack_AlreadyDestroyed_IsIgnoredAndDoesNotRecordAgain()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Breakable");
        try
        {
            PersistentBreakable breakable = SetUpBreakable(go, "breakable_6", registry, PersistenceLifetime.Permanent);
            InvokePrivate(breakable, "Awake");

            HeroAttackHit hit = new HeroAttackHit(null, HeroAttackDirection.Side, 5, Vector2.zero, Vector2.zero);
            HeroAttackResult first = breakable.ReceiveHeroAttack(hit);
            HeroAttackResult second = breakable.ReceiveHeroAttack(hit);

            Assert.That(first.WasAccepted, Is.True, "The confirmed destroying hit must be accepted.");
            Assert.That(second.Outcome, Is.EqualTo(HeroAttackOutcome.Ignored), "A hit on an already-destroyed breakable must be ignored -- only confirmed destruction records state, once.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void ReceiveHeroAttack_NoRegistryAssigned_DestroysLocallyButRecordsNothing()
    {
        GameObject go = new GameObject("Breakable");
        try
        {
            PersistentBreakable breakable = SetUpBreakable(go, "breakable_7", null, PersistenceLifetime.Permanent);
            InvokePrivate(breakable, "Awake");

            HeroAttackHit hit = new HeroAttackHit(null, HeroAttackDirection.Side, 5, Vector2.zero, Vector2.zero);
            HeroAttackResult result = breakable.ReceiveHeroAttack(hit);

            Assert.That(result.WasAccepted, Is.True);
            Collider2D collider = (Collider2D)GetPrivateField(breakable, "solidCollider");
            Assert.That(collider.enabled, Is.False, "Destruction still applies locally even without a registry to persist to.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Awake_UnknownStoredValue_LogsWarningAndFallsBackToIntact()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Breakable");
        try
        {
            registry.SetObjectState("breakable_8", "garbled");
            PersistentBreakable breakable = SetUpBreakable(go, "breakable_8", registry, PersistenceLifetime.Permanent);
            Collider2D collider = (Collider2D)GetPrivateField(breakable, "solidCollider");

            LogAssert.Expect(LogType.Warning, new Regex("unrecognized stored state"));
            InvokePrivate(breakable, "Awake");

            Assert.That(collider.enabled, Is.True, "Unknown stored values must fall back to the authored default (intact).");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    private static PersistentBreakable SetUpBreakable(GameObject go, string worldObjectId, WorldStateRegistry registry, PersistenceLifetime lifetime)
    {
        go.SetActive(false); // prevent Unity's own Awake pass; we invoke the target method directly
        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.isTrigger = false;

        // intactVisualRoot must be a separate child, never the component's own root GameObject:
        // toggling the root's own active state from inside its own Awake (which this test harness's
        // "start inactive, invoke Awake manually" isolation technique relies on) would transition it
        // from inactive to active mid-Awake and cause Unity to re-enter Awake recursively.
        GameObject visualGo = new GameObject("Visual");
        visualGo.transform.SetParent(go.transform);

        PersistentBreakable breakable = go.AddComponent<PersistentBreakable>();
        SetPrivateField(breakable, "worldObjectId", worldObjectId);
        SetPrivateField(breakable, "registry", registry);
        SetPrivateField(breakable, "lifetime", lifetime);
        SetPrivateField(breakable, "solidCollider", collider);
        SetPrivateField(breakable, "intactVisualRoot", visualGo);

        return breakable;
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on '{target.GetType().Name}'.");
        return field.GetValue(target);
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
