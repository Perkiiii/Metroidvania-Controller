using System.Reflection;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class UndeadExecutionerBehaviourTests
{
    private GameObject root;
    private UndeadExecutionerConfig bossConfig;
    private EnemyConfig enemyConfig;
    private UndeadExecutionerSpiritPressure spiritPressure;

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

        Assert.That(behaviour.TryPrepareForEncounter(), Is.True);

        Assert.That(body.gravityScale, Is.Zero);
        Assert.That(body.constraints.HasFlag(RigidbodyConstraints2D.FreezePositionY), Is.True);
        Assert.That(body.constraints.HasFlag(RigidbodyConstraints2D.FreezeRotation), Is.True);
        Assert.That(behaviour.AuthoredHoverY, Is.EqualTo(body.position.y));
        Assert.That(typeof(UndeadExecutionerBehaviour).GetField("IsGrounded"), Is.Null);
    }

    [Test]
    public void MissingRequiredReferenceFailsPreparation()
    {
        UndeadExecutionerBehaviour behaviour = CreateBehaviour();
        SetField(behaviour, "config", null);

        LogAssert.Expect(
            LogType.Error,
            "[UndeadExecutionerBehaviour] 'Undead Executioner Test' is missing required foundation, config, motor, health, or Animancer references.");

        Assert.That(behaviour.TryPrepareForEncounter(), Is.False);
        Assert.That(behaviour.IsPrepared, Is.False);
        Assert.That(behaviour.ComboFirst.IsAttackWindowActive, Is.False);
        Assert.That(behaviour.ComboSecond.IsAttackWindowActive, Is.False);
        Assert.That(behaviour.ShadowBurst.IsAttackWindowActive, Is.False);
    }

    [Test]
    public void InvalidAttackTimingFailsPreparationAndClosesExistingAttackWindow()
    {
        UndeadExecutionerBehaviour behaviour = CreateBehaviour();
        Assert.That(behaviour.ComboFirst.BeginAttack(), Is.True);
        behaviour.ComboFirst.OpenAttackWindow();
        Assert.That(behaviour.ComboFirst.IsAttackWindowActive, Is.True);

        UndeadExecutionerConfig.AttackTiming invalid = bossConfig.comboFirstTiming;
        invalid.startup = -1f;
        bossConfig.comboFirstTiming = invalid;
        LogAssert.Expect(
            LogType.Error,
            "[UndeadExecutionerBehaviour] 'Undead Executioner Test' could not configure all authored attack controllers.");

        Assert.That(behaviour.TryPrepareForEncounter(), Is.False);
        Assert.That(behaviour.IsPrepared, Is.False);
        Assert.That(behaviour.ComboFirst.IsAttackWindowActive, Is.False);
        Assert.That(behaviour.ComboFirst.IsAttacking, Is.False);
        Assert.That(behaviour.ComboSecond.IsAttackWindowActive, Is.False);
        Assert.That(behaviour.ShadowBurst.IsAttackWindowActive, Is.False);
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

        Assert.That(behaviour.TryPrepareForEncounter(), Is.True);
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
        Assert.That(behaviour.TryPrepareForEncounter(), Is.True);
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

        Assert.That(behaviour.TryPrepareForEncounter(), Is.True);
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

    [Test]
    public void GlideReachingToleranceStopsMovementAndReturnsToNeutral()
    {
        UndeadExecutionerBehaviour behaviour = CreateBehaviour();
        bossConfig.introDuration = 0f;
        bossConfig.preferredDistanceMinimum = 3f;
        bossConfig.preferredDistanceMaximum = 5f;
        bossConfig.glideMaximumDistance = 4f;
        bossConfig.glideMinimumDistance = 2f;
        Rigidbody2D body = root.GetComponent<Rigidbody2D>();

        Assert.That(behaviour.TryPrepareForEncounter(), Is.True);
        behaviour.PlayIntro();
        InvokePrivate(behaviour, "Update");
        behaviour.BeginCombat();

        // No arena limits are assigned, no EnemyController is present (enemyConfig stays null on
        // the behaviour), so HasHorizontalObstruction is always false here -- this exercises only
        // the tolerance/stop path, not obstruction handling.
        InvokePrivate(behaviour, "TryBeginGlide", root.transform.position.x + 10f, false);
        Assert.That(behaviour.State, Is.EqualTo(UndeadExecutionerState.Repositioning));

        // currentX(0) + direction(1) * min(desiredX=10-4=6, maxTarget=0+4=4) = 4, per the same
        // math TryBeginGlide itself runs. Placing the body exactly there (as real physics
        // integration eventually would) exercises FixedUpdate's stop-tolerance branch directly,
        // matching this test suite's existing style of driving state transitions explicitly
        // rather than stepping Physics2D simulation frame by frame.
        body.position = new Vector2(4f, body.position.y);
        InvokePrivate(behaviour, "FixedUpdate");

        Assert.That(behaviour.State, Is.EqualTo(UndeadExecutionerState.Neutral));
        Assert.That(body.linearVelocity.x, Is.Zero);
    }

    [Test]
    public void InterruptingDuringSpiritActionDeactivatesSpiritAndIgnoresLateCompletion()
    {
        UndeadExecutionerBehaviour behaviour = CreateBehaviour(withSpirit: true);
        bossConfig.introDuration = 0f;
        Assert.That(behaviour.TryPrepareForEncounter(), Is.True);
        behaviour.PlayIntro();
        InvokePrivate(behaviour, "Update");
        behaviour.BeginCombat();

        InvokePrivate(behaviour, "BeginSpiritPressure", root.transform.position.x);
        Assert.That(behaviour.State, Is.EqualTo(UndeadExecutionerState.SpiritAction));
        Assert.That(spiritPressure.IsActive, Is.True);

        // The coroutine-driven appear/idle/attack sequence cannot progress past its first
        // WaitForSeconds synchronously in an EditMode test, so open the spirit's own attack
        // window directly to simulate "mid-pressure-window" -- exactly the state
        // InterruptEncounter must be able to close.
        Assert.That(spiritPressure.AttackController.BeginAttack(), Is.True);

        behaviour.InterruptEncounter();

        Assert.That(behaviour.State, Is.EqualTo(UndeadExecutionerState.Interrupted));
        Assert.That(spiritPressure.IsActive, Is.False, "Interruption must deactivate the spirit.");
        Assert.That(spiritPressure.AttackController.IsAttacking, Is.False, "Interruption must close the spirit's attack window.");

        // A late/duplicate Completed signal (e.g. a stray coroutine tail) must not resurrect
        // Neutral out from under an already-interrupted encounter.
        InvokePrivate(behaviour, "HandleSpiritCompleted");
        Assert.That(behaviour.State, Is.EqualTo(UndeadExecutionerState.Interrupted));
    }

    [Test]
    public void ComboSecondBeginFailureClosesTheActionSafelyAndReturnsToNeutral()
    {
        UndeadExecutionerBehaviour behaviour = CreateBehaviour();
        bossConfig.introDuration = 0f;
        bossConfig.executionerComboClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/_Project/Animations/Bosses/UndeadExecutioner/UndeadExecutionerCombo.anim");
        Assert.That(bossConfig.executionerComboClip, Is.Not.Null);

        Assert.That(behaviour.TryPrepareForEncounter(), Is.True);
        behaviour.PlayIntro();
        InvokePrivate(behaviour, "Update");
        behaviour.BeginCombat();

        InvokePrivate(behaviour, "BeginExecutionerCombo");
        Assert.That(behaviour.State, Is.EqualTo(UndeadExecutionerState.ExecutionerCombo));

        behaviour.HandleComboFirstOpen();
        behaviour.HandleComboFirstClose();

        // Force the boss's own ComboSecond.BeginAttack() call to fail by consuming ComboSecond's
        // Idle state out from under it first (CanStartAttack requires phase == Idle).
        Assert.That(behaviour.ComboSecond.BeginAttack(), Is.True);
        behaviour.HandleComboSecondBegin();

        Assert.That(behaviour.State, Is.EqualTo(UndeadExecutionerState.Neutral));
        Assert.That(behaviour.CurrentAttack, Is.EqualTo(UndeadExecutionerAttack.None));
        Assert.That(behaviour.ComboSecond.Phase, Is.EqualTo(EnemyAttackPhase.Idle));
        Assert.That(behaviour.ComboSecond.IsAttackWindowActive, Is.False);
    }

    private UndeadExecutionerBehaviour CreateBehaviour(bool withSpirit = false)
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

        // Must be wired before Initialize()/OnEnable() run SubscribeEvents(), which only
        // subscribes to spiritPressure.Completed once and never re-subscribes on a later
        // assignment (matches real authoring, where spiritPressure is assigned in the Inspector
        // before the object is ever enabled).
        if (withSpirit)
        {
            spiritPressure = CreateSpiritPressure(blackboard, motor);
            SetField(behaviour, "spiritPressure", spiritPressure);
        }

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

    private UndeadExecutionerSpiritPressure CreateSpiritPressure(EnemyStateBlackboard blackboard, EnemyMotor motor)
    {
        GameObject spiritObject = new GameObject("Spirit");
        spiritObject.transform.SetParent(root.transform);
        UndeadExecutionerSpiritPressure spirit = spiritObject.AddComponent<UndeadExecutionerSpiritPressure>();
        EnemyAttackController attack = AddAttack("SpiritAttack", blackboard, motor);
        Animator spiritAnimator = spiritObject.AddComponent<Animator>();
        AnimancerComponent spiritAnimancer = spiritObject.AddComponent<AnimancerComponent>();
        spiritAnimancer.Animator = spiritAnimator;
        SetField(spirit, "attackController", attack);
        SetField(spirit, "animancer", spiritAnimancer);
        return spirit;
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
