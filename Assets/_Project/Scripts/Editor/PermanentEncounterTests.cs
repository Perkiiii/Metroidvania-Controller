using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// End-to-end coverage for EnemyPersistenceMode.PermanentEncounter, going through the same
// EnemyController one-time initialization path real placed enemies use (not just the
// EnemyPersistence unit-level checks already covered by EnemyPersistenceTests).
public sealed class PermanentEncounterTests
{
    private sealed class TestEnemyBehaviour : MonoBehaviour, IEnemyBehaviour
    {
        public bool InitializeCalled;

        public void Initialize(EnemyStateBlackboard blackboard, Rigidbody2D body)
        {
            InitializeCalled = true;
        }
    }

    [Test]
    public void ActiveWhenNotDefeated_InitializesFullyAndSubscribesDeathRecording()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject go = new GameObject("Boss Active");
        try
        {
            go.SetActive(false);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            EnemyMotor motor = go.AddComponent<EnemyMotor>();
            TestEnemyBehaviour behaviour = go.AddComponent<TestEnemyBehaviour>();

            EnemyPersistence persistence = go.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "worldObjectId", "boss_active");
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.PermanentEncounter);
            SetPrivateField(persistence, "registry", registry);

            EnemyController controller = go.AddComponent<EnemyController>();
            SetPrivateField(controller, "config", config);

            InvokePrivate(controller, "Awake");

            EnemyStateBlackboard blackboard = go.GetComponent<EnemyStateBlackboard>();
            Assert.That(blackboard.dead, Is.False);
            Assert.That(blackboard.suppressed, Is.False);
            Assert.That(collider.enabled, Is.True);
            Assert.That(body.simulated, Is.True);
            Assert.That(motor.enabled, Is.True);
            Assert.That(behaviour.InitializeCalled, Is.True);

            EnemyHealthComponent health = go.GetComponent<EnemyHealthComponent>();
            FieldInfo onDeathField = typeof(EnemyHealthComponent).GetField("OnDeath", BindingFlags.Instance | BindingFlags.NonPublic);
            System.Delegate onDeath = (System.Delegate)onDeathField.GetValue(health);
            Assert.That(onDeath, Is.Not.Null, "Active PermanentEncounter enemy must subscribe persistence to confirmed death.");

            bool subscribed = false;
            foreach (System.Delegate d in onDeath.GetInvocationList())
            {
                if (ReferenceEquals(d.Target, persistence) && d.Method.Name == nameof(EnemyPersistence.RecordDeath))
                    subscribed = true;
            }
            Assert.That(subscribed, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void SuppressedWhenAlreadyDefeated_DisablesGameplaySystemsWithoutInitializingBehaviour()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject go = new GameObject("Boss Suppressed");
        try
        {
            registry.MarkEncounterDefeated("boss_defeated");

            go.SetActive(false);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            EnemyMotor motor = go.AddComponent<EnemyMotor>();
            TestEnemyBehaviour behaviour = go.AddComponent<TestEnemyBehaviour>();

            EnemyPersistence persistence = go.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "worldObjectId", "boss_defeated");
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.PermanentEncounter);
            SetPrivateField(persistence, "registry", registry);

            EnemyController controller = go.AddComponent<EnemyController>();
            SetPrivateField(controller, "config", config);

            InvokePrivate(controller, "Awake");

            EnemyStateBlackboard blackboard = go.GetComponent<EnemyStateBlackboard>();
            Assert.That(blackboard.dead, Is.True);
            Assert.That(blackboard.suppressed, Is.True);
            Assert.That(body.simulated, Is.False);
            Assert.That(collider.enabled, Is.False);
            Assert.That(renderer.enabled, Is.False);
            Assert.That(motor.enabled, Is.False);
            Assert.That(behaviour.enabled, Is.False);
            Assert.That(behaviour.InitializeCalled, Is.False, "Suppressed PermanentEncounter enemies must never initialize IEnemyBehaviour.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void ConfirmedDeath_RecordsDefeatWithoutCreatingATimedRecord()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject go = new GameObject("Boss");
        try
        {
            EnemyPersistence persistence = go.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "worldObjectId", "boss_death");
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.PermanentEncounter);
            SetPrivateField(persistence, "registry", registry);
            persistence.Initialize(config);

            Assert.That(registry.IsEncounterDefeated("boss_death"), Is.False, "Precondition: not yet defeated.");

            persistence.RecordDeath();

            Assert.That(registry.IsEncounterDefeated("boss_death"), Is.True);
            Assert.That(InvokeShouldSuppress(registry, "boss_death"), Is.False,
                "PermanentEncounter confirmed death must never create a timed respawnable-enemy record.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void RepeatedCompletionRecording_IsIdempotent()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject go = new GameObject("Boss");
        try
        {
            EnemyPersistence persistence = go.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "worldObjectId", "boss_repeat");
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.PermanentEncounter);
            SetPrivateField(persistence, "registry", registry);
            persistence.Initialize(config);

            persistence.RecordDeath();
            persistence.RecordDeath();

            Assert.That(registry.IsEncounterDefeated("boss_repeat"), Is.True);
            Assert.That(InvokeShouldSuppress(registry, "boss_repeat"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void FullLifecycle_DeathThenSimulatedSceneReloadSuppressesNextInstance()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject firstGo = new GameObject("Boss Instance A");
        GameObject secondGo = new GameObject("Boss Instance B");
        try
        {
            // Instance A: active, then confirmed death (as EnemyController would wire via
            // health.OnDeath += persistence.RecordDeath on the active initialization path).
            firstGo.SetActive(false);
            firstGo.AddComponent<Rigidbody2D>();
            firstGo.AddComponent<BoxCollider2D>();
            EnemyPersistence firstPersistence = firstGo.AddComponent<EnemyPersistence>();
            SetPrivateField(firstPersistence, "worldObjectId", "boss_lifecycle");
            SetPrivateField(firstPersistence, "mode", EnemyPersistenceMode.PermanentEncounter);
            SetPrivateField(firstPersistence, "registry", registry);
            EnemyController firstController = firstGo.AddComponent<EnemyController>();
            SetPrivateField(firstController, "config", config);
            InvokePrivate(firstController, "Awake");

            Assert.That(firstGo.GetComponent<EnemyStateBlackboard>().suppressed, Is.False,
                "Precondition: first instance initializes active because the encounter is not yet defeated.");

            firstPersistence.RecordDeath();
            Assert.That(registry.IsEncounterDefeated("boss_lifecycle"), Is.True);

            // Instance B: a fresh EnemyController+EnemyPersistence sharing the same id/registry,
            // simulating that scene's next EnemyController.Awake after the checkpoint scene reloads.
            secondGo.SetActive(false);
            secondGo.AddComponent<Rigidbody2D>();
            secondGo.AddComponent<BoxCollider2D>();
            EnemyPersistence secondPersistence = secondGo.AddComponent<EnemyPersistence>();
            SetPrivateField(secondPersistence, "worldObjectId", "boss_lifecycle");
            SetPrivateField(secondPersistence, "mode", EnemyPersistenceMode.PermanentEncounter);
            SetPrivateField(secondPersistence, "registry", registry);
            EnemyController secondController = secondGo.AddComponent<EnemyController>();
            SetPrivateField(secondController, "config", config);
            InvokePrivate(secondController, "Awake");

            EnemyStateBlackboard secondBlackboard = secondGo.GetComponent<EnemyStateBlackboard>();
            Assert.That(secondBlackboard.dead, Is.True);
            Assert.That(secondBlackboard.suppressed, Is.True,
                "The next scene initialization for the same encounter id must be suppressed once defeated.");
        }
        finally
        {
            Object.DestroyImmediate(firstGo);
            Object.DestroyImmediate(secondGo);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void SaveLoadRoundTrip_PreservesCompletionAndNeverLeaksATimedRecord()
    {
        WorldStateRegistry source = ScriptableObject.CreateInstance<WorldStateRegistry>();
        WorldStateRegistry destination = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject go = new GameObject("Boss");
        try
        {
            EnemyPersistence persistence = go.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "worldObjectId", "boss_saveload");
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.PermanentEncounter);
            SetPrivateField(persistence, "registry", source);
            persistence.Initialize(config);
            persistence.RecordDeath();

            SaveData data = new SaveData();
            source.GatherSaveData(data);

            Assert.That(data.world.defeatedEncounterIds, Contains.Item("boss_saveload"));

            destination.ApplySaveData(data);

            Assert.That(destination.IsEncounterDefeated("boss_saveload"), Is.True);
            Assert.That(InvokeShouldSuppress(destination, "boss_saveload"), Is.False,
                "Loading must never resurrect a leaked timed record for a permanently defeated encounter -- " +
                "WorldSaveData has no field for timed records at all, so there is nothing to round-trip there.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(destination);
        }
    }

    [Test]
    public void NormalDeathTransientReset_DoesNotClearPermanentCompletion()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject go = new GameObject("Boss");
        try
        {
            EnemyPersistence persistence = go.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "worldObjectId", "boss_normal_death");
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.PermanentEncounter);
            SetPrivateField(persistence, "registry", registry);
            persistence.Initialize(config);
            persistence.RecordDeath();

            // Exactly the two calls GameManager.ApplyNormalDeathRespawn makes on every normal death.
            registry.ResetRespawnableEnemyDeaths();
            registry.ResetUntilDeathState();

            Assert.That(registry.IsEncounterDefeated("boss_normal_death"), Is.True,
                "Normal-death transient resets must never clear permanent encounter completion.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(registry);
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
