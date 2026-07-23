using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GameManagerWorldPersistenceLifecycleTests
{
    // BeginRespawnSequence is the single entry point for every normal death (see
    // HeroController.HandleDeath) and is now also the single authoritative point where transient
    // world state is reset -- exactly once, before any scene-transition or in-place-fallback
    // coroutine is started, so freshly initializing scene objects (e.g. EnemyController) observe
    // cleared state. ApplyNormalDeathRespawn no longer resets anything; it only runs after the
    // checkpoint scene has already loaded (or, for the fallback, after the reset already ran).
    //
    // These tests never let GameManager.Awake() run (the GameObject is created inactive and
    // dependencies are injected via reflection instead), to avoid the Instance/DontDestroyOnLoad
    // statics that only behave correctly inside Play Mode. Because of that, and because
    // SaveManager.Instance is never set up, the respawn scene always resolves to an empty string
    // and is therefore never loadable here -- every call below to BeginRespawnSequence takes the
    // in-place-fallback branch and every call to BeginHazardRecoverySequence follows its own single
    // coroutine-start path. Both branches always end by calling StartCoroutine, which Unity refuses
    // to run on this inactive GameObject and reports as a single Console error (not a catchable
    // exception) -- expected explicitly below via LogAssert so it doesn't fail the test, and relied
    // upon as the point past which no further production code in that call runs.

    private static readonly Regex CoroutineCouldNotStart = new Regex("Coroutine couldn't be started");

    // Once a test calls LogAssert.Expect, Unity's TestRunner requires every subsequent
    // Warning/Error/Exception log in that test to be explicitly expected, in order. Every
    // BeginRespawnSequence call here resolves an empty, unloadable respawn scene (SaveManager
    // isn't set up), so it always logs this warning immediately before the coroutine-start error.
    private static readonly Regex RespawnSceneNotLoadable = new Regex("is not loadable");

    [Test]
    public void BeginRespawnSequence_ClearsTransientWorldStateBeforeAttemptingSceneTransitionOrFallback()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("GameManager Test");
        try
        {
            registry.RecordRespawnableEnemyDeath("enemy_1", 9999f);
            registry.SetUntilDeathState("lever_1", "raised");

            GameManager gm = CreateUninitializedGameManager(go, registry);

            // BeginRespawnSequence always ends by starting a coroutine (scene-transition or the
            // in-place fallback); on this inactive GameObject that start always fails, before the
            // coroutine body runs. If the reset were deferred until after a successful
            // scene-transition/fallback start -- the bug this test guards against -- it would never
            // run at all here, since the coroutine-start attempt always fails first.
            LogAssert.Expect(LogType.Warning, RespawnSceneNotLoadable);
            LogAssert.Expect(LogType.Error, CoroutineCouldNotStart);
            gm.BeginRespawnSequence();
            LogAssert.NoUnexpectedReceived();

            Assert.That(InvokeShouldSuppress(registry, "enemy_1"), Is.False,
                "Transient enemy death records must already be cleared before any scene-transition/fallback attempt.");
            Assert.That(registry.TryGetUntilDeathState("lever_1", out _), Is.False,
                "Generic until-death state must already be cleared before any scene-transition/fallback attempt.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void BeginRespawnSequence_PreservesPermanentWorldState()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("GameManager Test");
        try
        {
            registry.MarkEncounterDefeated("boss_1");
            registry.MarkPickupCollected("pickup_1");
            registry.MarkRoomVisited("room_1");
            registry.SetObjectState("door_1", "open");

            GameManager gm = CreateUninitializedGameManager(go, registry);

            LogAssert.Expect(LogType.Warning, RespawnSceneNotLoadable);
            LogAssert.Expect(LogType.Error, CoroutineCouldNotStart);
            gm.BeginRespawnSequence();
            LogAssert.NoUnexpectedReceived();

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
    public void BeginRespawnSequence_MissingRegistry_DoesNotThrowDuringReset()
    {
        GameObject go = new GameObject("GameManager Test");
        try
        {
            // worldStateRegistry intentionally left unassigned.
            GameManager gm = CreateUninitializedGameManager(go, null);

            // The only Console error this call may produce is the coroutine-start failure below;
            // a null registry reaching the reset would instead surface as an unhandled
            // NullReferenceException here, which LogAssert.NoUnexpectedReceived would catch.
            LogAssert.Expect(LogType.Warning, RespawnSceneNotLoadable);
            LogAssert.Expect(LogType.Error, CoroutineCouldNotStart);
            Assert.DoesNotThrow(() => gm.BeginRespawnSequence());
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BeginRespawnSequence_SecondCallWhileRespawnInProgress_DoesNotResetAgain()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("GameManager Test");
        try
        {
            registry.RecordRespawnableEnemyDeath("enemy_1", 9999f);

            GameManager gm = CreateUninitializedGameManager(go, registry);

            // First call resets, then fails to start its coroutine on this inactive GameObject.
            // _respawnOrRecoveryInProgress is left true because that coroutine's body (and its own
            // cleanup) never actually ran -- exactly the guard state a real in-flight respawn would
            // leave behind while its scene transition or fallback is still underway.
            LogAssert.Expect(LogType.Warning, RespawnSceneNotLoadable);
            LogAssert.Expect(LogType.Error, CoroutineCouldNotStart);
            gm.BeginRespawnSequence();
            Assert.That(InvokeShouldSuppress(registry, "enemy_1"), Is.False, "Precondition: first call already reset the registry.");

            // A death recorded after the first respawn began must survive a second call while that
            // guard is still set -- proving the in-progress guard blocks a re-entrant reset (and a
            // second coroutine-start attempt), rather than the reset simply being idempotent.
            registry.RecordRespawnableEnemyDeath("enemy_2", 9999f);
            gm.BeginRespawnSequence();
            LogAssert.NoUnexpectedReceived();

            Assert.That(InvokeShouldSuppress(registry, "enemy_2"), Is.True,
                "BeginRespawnSequence must not run the reset again while a respawn is already in progress.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void BeginHazardRecoverySequence_DoesNotClearTransientWorldState()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("GameManager Test");
        try
        {
            registry.RecordRespawnableEnemyDeath("enemy_1", 9999f);
            registry.SetUntilDeathState("lever_1", "raised");

            GameManager gm = CreateUninitializedGameManager(go, registry);
            HazardContact contact = new HazardContact(1, HazardRecoveryMode.RecoverLocal, null, null, null);

            // BeginHazardRecoverySequence always starts a coroutine; on this inactive GameObject
            // that fails the same way. Recoverable hazard reposition has no reference to
            // WorldStateRegistry anywhere in GameManager, so the registry must be untouched
            // regardless of where the coroutine-start failure cuts execution off.
            LogAssert.Expect(LogType.Error, CoroutineCouldNotStart);
            gm.BeginHazardRecoverySequence(contact);
            LogAssert.NoUnexpectedReceived();

            Assert.That(InvokeShouldSuppress(registry, "enemy_1"), Is.True,
                "Recoverable hazard reposition must never clear timed enemy death records.");
            Assert.That(registry.TryGetUntilDeathState("lever_1", out string state), Is.True,
                "Recoverable hazard reposition must never clear generic until-death state.");
            Assert.That(state, Is.EqualTo("raised"));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    // Creates a GameManager whose Awake() never runs (the GameObject is inactive when the
    // component is added), then injects only the dependencies BeginRespawnSequence/
    // BeginHazardRecoverySequence need directly via reflection. This avoids the
    // Instance/DontDestroyOnLoad singleton machinery, which does not behave correctly when driven
    // outside Play Mode.
    private static GameManager CreateUninitializedGameManager(GameObject go, WorldStateRegistry registry)
    {
        go.SetActive(false);
        GameManager gm = go.AddComponent<GameManager>();

        if (registry != null)
            SetPrivateField(gm, "worldStateRegistry", registry);

        SceneLoader sceneLoader = new SceneLoader();
        SetPrivateField(gm, "_sceneLoader", sceneLoader);
        SetPrivateField(gm, "_sceneTransitionManager", new SceneTransitionManager(gm, sceneLoader));

        return gm;
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
}
