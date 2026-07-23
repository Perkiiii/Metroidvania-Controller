using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EnemyPersistenceTests
{
    private sealed class TestEnemyBehaviour : MonoBehaviour, IEnemyBehaviour
    {
        public bool InitializeCalled;

        public void Initialize(EnemyStateBlackboard blackboard, Rigidbody2D body)
        {
            InitializeCalled = true;
        }
    }

    // -------------------------------------------------------------------------
    // EnemyPersistence unit tests
    // -------------------------------------------------------------------------

    [Test]
    public void Initialize_MissingRegistry_LogsErrorAndNeverSuppresses()
    {
        GameObject go = new GameObject("Persistence Test");
        try
        {
            EnemyPersistence persistence = go.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "worldObjectId", "e1");
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.RespawnableTimed);

            EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
            try
            {
                config.respawnDuration = 10f;
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no WorldStateRegistry assigned"));
                persistence.Initialize(config);

                Assert.That(persistence.ShouldSuppressOnInitialization(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ShouldSuppressOnInitialization_RoomRuntime_AlwaysFalseEvenWithDeathRecord()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject go = new GameObject("Persistence Test");
        try
        {
            config.respawnDuration = 10f;
            registry.RecordRespawnableEnemyDeath("e_room", 9999f);

            EnemyPersistence persistence = go.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "worldObjectId", "e_room");
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.RoomRuntime);
            SetPrivateField(persistence, "registry", registry);
            persistence.Initialize(config);

            Assert.That(persistence.ShouldSuppressOnInitialization(), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void ShouldSuppressOnInitialization_RespawnableTimed_TrueWhileRecordValidFalseAfterReset()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject go = new GameObject("Persistence Test");
        try
        {
            config.respawnDuration = 10f;

            EnemyPersistence persistence = go.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "worldObjectId", "e_timed");
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.RespawnableTimed);
            SetPrivateField(persistence, "registry", registry);
            persistence.Initialize(config);

            Assert.That(persistence.ShouldSuppressOnInitialization(), Is.False, "No death record yet.");

            registry.RecordRespawnableEnemyDeath("e_timed", 9999f);
            Assert.That(persistence.ShouldSuppressOnInitialization(), Is.True);

            registry.ResetRespawnableEnemyDeaths();
            Assert.That(persistence.ShouldSuppressOnInitialization(), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void ShouldSuppressOnInitialization_PermanentEncounter_TrueOnlyWhenDefeated()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject go = new GameObject("Persistence Test");
        try
        {
            EnemyPersistence persistence = go.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "worldObjectId", "boss_1");
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.PermanentEncounter);
            SetPrivateField(persistence, "registry", registry);
            persistence.Initialize(config);

            Assert.That(persistence.ShouldSuppressOnInitialization(), Is.False);

            registry.MarkEncounterDefeated("boss_1");
            Assert.That(persistence.ShouldSuppressOnInitialization(), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void RecordDeath_MapsEachModeToTheCorrectRegistryCall()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject roomGo = new GameObject("Room Runtime");
        GameObject timedGo = new GameObject("Respawnable Timed");
        GameObject bossGo = new GameObject("Permanent Encounter");
        try
        {
            config.respawnDuration = 42f;

            EnemyPersistence roomPersistence = SetUpPersistence(roomGo, "room_enemy", EnemyPersistenceMode.RoomRuntime, registry, config);
            EnemyPersistence timedPersistence = SetUpPersistence(timedGo, "timed_enemy", EnemyPersistenceMode.RespawnableTimed, registry, config);
            EnemyPersistence bossPersistence = SetUpPersistence(bossGo, "boss_enemy", EnemyPersistenceMode.PermanentEncounter, registry, config);

            roomPersistence.RecordDeath();
            timedPersistence.RecordDeath();
            bossPersistence.RecordDeath();

            Assert.That(InvokeShouldSuppress(registry, "room_enemy"), Is.False, "RoomRuntime must record nothing.");
            Assert.That(InvokeShouldSuppress(registry, "timed_enemy"), Is.True);
            Assert.That(registry.IsEncounterDefeated("boss_enemy"), Is.True);
            Assert.That(InvokeShouldSuppress(registry, "boss_enemy"), Is.False, "PermanentEncounter must never create a timed record.");
        }
        finally
        {
            Object.DestroyImmediate(roomGo);
            Object.DestroyImmediate(timedGo);
            Object.DestroyImmediate(bossGo);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(registry);
        }
    }

    // -------------------------------------------------------------------------
    // EnemyController one-time initialization gating
    // -------------------------------------------------------------------------

    [Test]
    public void Awake_SuppressedRespawnableTimed_DisablesPhysicsAndCombatWithoutInitializingBehaviour()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject go = new GameObject("Enemy Suppressed");
        try
        {
            config.respawnDuration = 10f;
            registry.RecordRespawnableEnemyDeath("suppressed_enemy", 9999f);

            go.SetActive(false); // prevent Unity's own Awake pass; we invoke it manually below
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            EnemyMotor motor = go.AddComponent<EnemyMotor>();
            TestEnemyBehaviour behaviour = go.AddComponent<TestEnemyBehaviour>();

            EnemyPersistence persistence = go.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "worldObjectId", "suppressed_enemy");
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.RespawnableTimed);
            SetPrivateField(persistence, "registry", registry);

            EnemyController controller = go.AddComponent<EnemyController>();
            SetPrivateField(controller, "config", config);

            InvokePrivate(controller, "Awake");

            EnemyStateBlackboard blackboard = go.GetComponent<EnemyStateBlackboard>();
            Assert.That(blackboard, Is.Not.Null);
            Assert.That(blackboard.dead, Is.True);
            Assert.That(blackboard.suppressed, Is.True);

            Assert.That(body.simulated, Is.False);
            Assert.That(collider.enabled, Is.False);
            Assert.That(renderer.enabled, Is.False);
            Assert.That(motor.enabled, Is.False);
            Assert.That(behaviour.enabled, Is.False);
            Assert.That(behaviour.InitializeCalled, Is.False, "Suppressed enemies must never initialize IEnemyBehaviour.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Awake_ActiveEnemy_InitializesBehaviourAndSubscribesDeathRecording()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject go = new GameObject("Enemy Active");
        try
        {
            config.respawnDuration = 10f;

            go.SetActive(false);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            EnemyMotor motor = go.AddComponent<EnemyMotor>();
            TestEnemyBehaviour behaviour = go.AddComponent<TestEnemyBehaviour>();

            EnemyPersistence persistence = go.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "worldObjectId", "active_enemy");
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.RespawnableTimed);
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
            Assert.That(health, Is.Not.Null);
            FieldInfo onDeathField = typeof(EnemyHealthComponent).GetField("OnDeath", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onDeathField, Is.Not.Null, "EnemyHealthComponent.OnDeath backing field not found via reflection.");
            System.Delegate onDeath = (System.Delegate)onDeathField.GetValue(health);

            Assert.That(onDeath, Is.Not.Null, "EnemyController must subscribe the persistence participant to confirmed death.");
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

    private static EnemyPersistence SetUpPersistence(GameObject go, string id, EnemyPersistenceMode mode, WorldStateRegistry registry, EnemyConfig config)
    {
        EnemyPersistence persistence = go.AddComponent<EnemyPersistence>();
        SetPrivateField(persistence, "worldObjectId", id);
        SetPrivateField(persistence, "mode", mode);
        SetPrivateField(persistence, "registry", registry);
        persistence.Initialize(config);
        return persistence;
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
