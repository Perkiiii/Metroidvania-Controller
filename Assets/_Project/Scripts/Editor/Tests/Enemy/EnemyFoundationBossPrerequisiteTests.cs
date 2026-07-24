using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyFoundationBossPrerequisiteTests
{
    [Test]
    public void HealthSnapshot_IsReadableAfterInitialization()
    {
        using (EnemyFixture fixture = new EnemyFixture(5))
        {
            Assert.That(fixture.Health.IsInitialized, Is.True);
            Assert.That(fixture.Health.CurrentHealth, Is.EqualTo(5));
            Assert.That(fixture.Health.MaximumHealth, Is.EqualTo(5));
        }
    }

    [Test]
    public void NonLethalDamage_FiresHealthChangedBeforeNonLethalDamaged()
    {
        using (EnemyFixture fixture = new EnemyFixture(5))
        {
            string order = "";
            fixture.Health.OnHealthChanged += (current, maximum) =>
            {
                order += $"health:{current}/{maximum};";
            };
            fixture.Health.OnDamaged += () => order += "damaged;";
            fixture.Health.OnDeath += () => order += "death;";

            fixture.Health.ReceiveHeroAttack(CreateHit(2));

            Assert.That(order, Is.EqualTo("health:3/5;damaged;"));
        }
    }

    [Test]
    public void LethalDamage_FiresHealthChangedBeforeDeathAndDoesNotFireDamaged()
    {
        using (EnemyFixture fixture = new EnemyFixture(3, EnemyDeathCleanupMode.RetainRoot))
        {
            string order = "";
            fixture.Health.OnHealthChanged += (current, maximum) =>
            {
                order += $"health:{current}/{maximum};";
            };
            fixture.Health.OnDamaged += () => order += "damaged;";
            fixture.Health.OnDeath += () => order += "death;";

            fixture.Health.ReceiveHeroAttack(CreateHit(3));

            Assert.That(order, Is.EqualTo("health:0/3;death;"));
            Assert.That(fixture.Root, Is.Not.Null);
        }
    }

    [Test]
    public void DefaultCleanupMode_IsDestroyAfterDelayWithoutInspectorMigration()
    {
        using (EnemyFixture fixture = new EnemyFixture(1))
        {
            Assert.That(fixture.Health.DeathCleanupMode, Is.EqualTo(EnemyDeathCleanupMode.DestroyAfterDelay));
        }
    }

    [Test]
    public void RetainRoot_KeepsRootAndPerformsTerminalShutdown()
    {
        using (EnemyFixture fixture = new EnemyFixture(1, EnemyDeathCleanupMode.RetainRoot))
        {
            fixture.Health.ReceiveHeroAttack(CreateHit(1));

            Assert.That(fixture.Root, Is.Not.Null);
            Assert.That(fixture.Body.bodyType, Is.EqualTo(RigidbodyType2D.Static));
            Assert.That(fixture.Collider.enabled, Is.False);
        }
    }

    [Test]
    public void EnemyController_InitializesEveryChildAttackControllerAndSharedBlackboardExcludesSibling()
    {
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject root = new GameObject("Enemy With Child Attacks");
        root.SetActive(false);

        try
        {
            root.AddComponent<Rigidbody2D>();
            root.AddComponent<BoxCollider2D>();
            root.AddComponent<EnemyMotor>();
            EnemyController controller = root.AddComponent<EnemyController>();
            SetPrivateField(controller, "config", config);

            EnemyAttackController first = AddChildAttack(root.transform, "Attack A");
            EnemyAttackController second = AddChildAttack(root.transform, "Attack B");

            InvokePrivate(controller, "Awake");

            Assert.That(GetPrivateField<bool>(first, "initialized"), Is.True);
            Assert.That(GetPrivateField<bool>(second, "initialized"), Is.True);
            Assert.That(first.BeginAttack(), Is.True);
            Assert.That(second.CanStartAttack, Is.False);
            Assert.That(second.BeginAttack(), Is.False);

            first.InterruptAttack(false);
            Assert.That(second.CanStartAttack, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void SuppressedInitialization_DisablesEveryChildAttackController()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        GameObject root = new GameObject("Suppressed Enemy With Child Attacks");
        root.SetActive(false);

        try
        {
            registry.MarkEncounterDefeated("suppressed_child_attacks");
            root.AddComponent<Rigidbody2D>();
            root.AddComponent<BoxCollider2D>();

            EnemyPersistence persistence = root.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "worldObjectId", "suppressed_child_attacks");
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.PermanentEncounter);
            SetPrivateField(persistence, "registry", registry);

            EnemyController controller = root.AddComponent<EnemyController>();
            SetPrivateField(controller, "config", config);
            EnemyAttackController first = AddChildAttack(root.transform, "Attack A");
            EnemyAttackController second = AddChildAttack(root.transform, "Attack B");

            InvokePrivate(controller, "Awake");

            Assert.That(first.enabled, Is.False);
            Assert.That(second.enabled, Is.False);
            Assert.That(GetPrivateField<bool>(first, "initialized"), Is.False);
            Assert.That(GetPrivateField<bool>(second, "initialized"), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(config);
            UnityEngine.Object.DestroyImmediate(registry);
        }
    }

    private static EnemyAttackController AddChildAttack(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent);
        return child.AddComponent<EnemyAttackController>();
    }

    private static HeroAttackHit CreateHit(int damage)
    {
        return new HeroAttackHit(null, HeroAttackDirection.Side, damage, Vector2.zero, Vector2.right);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
        return (T)field.GetValue(target);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on {target.GetType().Name}.");
        method.Invoke(target, null);
    }

    private sealed class EnemyFixture : IDisposable
    {
        public readonly GameObject Root;
        public readonly EnemyConfig Config;
        public readonly Rigidbody2D Body;
        public readonly BoxCollider2D Collider;
        public readonly EnemyHealthComponent Health;

        public EnemyFixture(int maximumHealth, EnemyDeathCleanupMode cleanupMode = EnemyDeathCleanupMode.DestroyAfterDelay)
        {
            Config = ScriptableObject.CreateInstance<EnemyConfig>();
            Config.maxHealth = maximumHealth;
            Config.deathDestroyDelay = 10f;

            Root = new GameObject("Enemy Health Fixture");
            EnemyStateBlackboard blackboard = Root.AddComponent<EnemyStateBlackboard>();
            Body = Root.AddComponent<Rigidbody2D>();
            Collider = Root.AddComponent<BoxCollider2D>();
            Health = Root.AddComponent<EnemyHealthComponent>();
            SetPrivateField(Health, "deathCleanupMode", cleanupMode);
            Health.Initialize(Config, blackboard, Body, null);
        }

        public void Dispose()
        {
            if (Root != null)
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }

            if (Config != null)
            {
                UnityEngine.Object.DestroyImmediate(Config);
            }
        }
    }
}
