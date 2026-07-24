using System.Reflection;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class UndeadExecutionerBehaviourTests
{
    private GameObject root;
    private UndeadExecutionerConfig bossConfig;
    private EnemyConfig enemyConfig;

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        if (bossConfig != null) Object.DestroyImmediate(bossConfig);
        if (enemyConfig != null) Object.DestroyImmediate(enemyConfig);
    }

    [Test]
    public void PrepareCreatesStableZeroGravityHoverWithoutGroundedDependency()
    {
        UndeadExecutionerBehaviour behaviour = CreateBehaviour();
        Rigidbody2D body = root.GetComponent<Rigidbody2D>();

        behaviour.PrepareForEncounter();

        Assert.That(body.gravityScale, Is.Zero);
        Assert.That(body.constraints.HasFlag(RigidbodyConstraints2D.FreezePositionY), Is.True);
        Assert.That(body.constraints.HasFlag(RigidbodyConstraints2D.FreezeRotation), Is.True);
        Assert.That(behaviour.AuthoredHoverY, Is.EqualTo(body.position.y));
        Assert.That(typeof(UndeadExecutionerBehaviour).GetField("IsGrounded"), Is.Null);
    }

    [Test]
    public void IntroAndDefeatPresentationSignalsAreIdempotent()
    {
        UndeadExecutionerBehaviour behaviour = CreateBehaviour();
        bossConfig.introDuration = 0f;
        int introCount = 0;
        int defeatCount = 0;
        behaviour.IntroCompleted += () => introCount++;
        behaviour.DefeatPresentationCompleted += () => defeatCount++;

        behaviour.PrepareForEncounter();
        behaviour.PlayIntro();
        InvokePrivate(behaviour, "Update");
        InvokePrivate(behaviour, "Update");
        behaviour.BeginCombat();
        InvokePrivate(behaviour, "HandleDeath");
        InvokePrivate(behaviour, "HandleDeath");

        Assert.That(introCount, Is.EqualTo(1));
        Assert.That(defeatCount, Is.EqualTo(1));
        Assert.That(behaviour.State, Is.EqualTo(UndeadExecutionerState.Defeated));
    }

    [Test]
    public void PhaseTransitionIsQueuedOnceAndInterruptionDoesNotCompleteIt()
    {
        UndeadExecutionerBehaviour behaviour = CreateBehaviour();
        bossConfig.introDuration = 0f;
        behaviour.PrepareForEncounter();
        behaviour.PlayIntro();
        InvokePrivate(behaviour, "Update");
        behaviour.BeginCombat();

        InvokePrivate(behaviour, "HandleHealthChanged", 12, 24);
        behaviour.InterruptEncounter();
        InvokePrivate(behaviour, "Update");

        Assert.That(behaviour.State, Is.EqualTo(UndeadExecutionerState.Interrupted));
        Assert.That(behaviour.IsPhaseTwo, Is.False);
    }

    [Test]
    public void CloseRangeDecisionUsesShadowBurstAndSuccessfulCompletionPreservesCooldown()
    {
        UndeadExecutionerBehaviour behaviour = CreateBehaviour();
        bossConfig.shadowBurstClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/_Project/Animations/Bosses/UndeadExecutioner/UndeadExecutionerShadowBurst.anim");
        Assert.That(bossConfig.shadowBurstClip, Is.Not.Null);
        bossConfig.introDuration = 0f;

        behaviour.PrepareForEncounter();
        behaviour.PlayIntro();
        InvokePrivate(behaviour, "Update");
        behaviour.BeginCombat();
        InvokePrivate(behaviour, "EvaluateNeutralDecision", root.transform.position.x + 1f);

        Assert.That(behaviour.State, Is.EqualTo(UndeadExecutionerState.ShadowBurst));
        Assert.That(behaviour.CurrentAttack, Is.EqualTo(UndeadExecutionerAttack.ShadowBurst));

        behaviour.HandleShadowBurstComplete();

        Assert.That(behaviour.State, Is.EqualTo(UndeadExecutionerState.Neutral));
        Assert.That(behaviour.ShadowBurst.IsOnCooldown, Is.True);
    }

    private UndeadExecutionerBehaviour CreateBehaviour()
    {
        root = new GameObject("Undead Executioner Test");
        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        EnemyStateBlackboard blackboard = root.AddComponent<EnemyStateBlackboard>();
        EnemyMotor motor = root.AddComponent<EnemyMotor>();
        EnemyHealthComponent health = root.AddComponent<EnemyHealthComponent>();
        EnemyRecoil recoil = root.AddComponent<EnemyRecoil>();
        EnemyPerception perception = root.AddComponent<EnemyPerception>();
        Animator animator = root.AddComponent<Animator>();
        AnimancerComponent animancer = root.AddComponent<AnimancerComponent>();
        animancer.Animator = animator;

        enemyConfig = ScriptableObject.CreateInstance<EnemyConfig>();
        enemyConfig.maxHealth = 24;
        motor.Initialize(enemyConfig, body);
        recoil.Initialize(enemyConfig, blackboard, body);
        health.Initialize(enemyConfig, blackboard, body, recoil);

        EnemyAttackController first = AddAttack("ComboFirst", blackboard, motor);
        EnemyAttackController second = AddAttack("ComboSecond", blackboard, motor);
        EnemyAttackController burst = AddAttack("ShadowBurst", blackboard, motor);

        bossConfig = ScriptableObject.CreateInstance<UndeadExecutionerConfig>();
        UndeadExecutionerBehaviour behaviour = root.AddComponent<UndeadExecutionerBehaviour>();
        SetField(behaviour, "config", bossConfig);
        SetField(behaviour, "motor", motor);
        SetField(behaviour, "perception", perception);
        SetField(behaviour, "health", health);
        SetField(behaviour, "bodyCollider", collider);
        SetField(behaviour, "animancer", animancer);
        SetField(behaviour, "comboFirst", first);
        SetField(behaviour, "comboSecond", second);
        SetField(behaviour, "shadowBurst", burst);
        behaviour.Initialize(blackboard, body);
        return behaviour;
    }

    private EnemyAttackController AddAttack(string name, EnemyStateBlackboard blackboard, EnemyMotor motor)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(root.transform);
        EnemyAttackController controller = child.AddComponent<EnemyAttackController>();
        controller.Initialize(enemyConfig, blackboard, motor);
        return controller;
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void InvokePrivate(object target, string name, params object[] args)
    {
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
