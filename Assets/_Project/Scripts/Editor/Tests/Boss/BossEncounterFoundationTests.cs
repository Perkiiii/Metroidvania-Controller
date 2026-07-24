using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BossEncounterFoundationTests
{
    private GameObject testRoot;
    private BossEncounterDefinition definition;
    private WorldStateRegistry registry;

    [SetUp]
    public void SetUp()
    {
        testRoot = new GameObject("Boss Encounter Test");
        testRoot.SetActive(false);
        definition = ScriptableObject.CreateInstance<BossEncounterDefinition>();
        registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        SetPrivateField(definition, "encounterId", "test_boss_encounter");
    }

    [TearDown]
    public void TearDown()
    {
        if (testRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(testRoot);
        }

        UnityEngine.Object.DestroyImmediate(definition);
        UnityEngine.Object.DestroyImmediate(registry);
    }

    [Test]
    public void CompletionWaitsForEveryDeathAndPresentationThenCommitsOnce()
    {
        TestRig rig = CreateRig(2);
        int bossesDefeatedCount = 0;
        int completedCount = 0;
        int registryNotificationCount = 0;
        rig.Controller.BossesDefeated += () => bossesDefeatedCount++;
        rig.Controller.EncounterCompleted += () => completedCount++;
        registry.Subscribe(
            new WorldStateKey(WorldStateCategory.DefeatedEncounter, definition.EncounterId),
            _ => registryNotificationCount++);

        Assert.That(rig.Controller.TryBeginEncounter(), Is.True);
        rig.Behaviours[0].CompleteIntro();
        rig.Behaviours[1].CompleteIntro();
        Assert.That(rig.Controller.State, Is.EqualTo(BossEncounterState.Active));

        InvokePrivate(rig.Participants[0], "HandleDefeated");
        rig.Behaviours[0].CompleteDefeatPresentation();
        Assert.That(registry.IsEncounterDefeated(definition.EncounterId), Is.False);

        InvokePrivate(rig.Participants[1], "HandleDefeated");
        Assert.That(rig.Controller.State, Is.EqualTo(BossEncounterState.BossesDefeated));
        Assert.That(bossesDefeatedCount, Is.EqualTo(1));
        Assert.That(registry.IsEncounterDefeated(definition.EncounterId), Is.False);

        rig.Behaviours[1].CompleteDefeatPresentation();
        rig.Behaviours[1].CompleteDefeatPresentation();

        Assert.That(rig.Controller.State, Is.EqualTo(BossEncounterState.Completed));
        Assert.That(rig.Controller.CompletionCommitted, Is.True);
        Assert.That(registry.IsEncounterDefeated(definition.EncounterId), Is.True);
        Assert.That(registryNotificationCount, Is.EqualTo(1));
        Assert.That(completedCount, Is.EqualTo(1));
        Assert.That(rig.Reward.activeSelf, Is.True);
        Assert.That(rig.Barrier.IsOpen, Is.True);
        Assert.That(rig.Behaviours[0].CompletionNotifications, Is.EqualTo(1));
        Assert.That(rig.Behaviours[1].CompletionNotifications, Is.EqualTo(1));
        Assert.That(rig.ActorRoots[0].activeSelf, Is.False);
        Assert.That(rig.ActorRoots[1].activeSelf, Is.False);
    }

    [Test]
    public void HeroDeathBeforeCommitInterruptsWithoutPersistenceOrReward()
    {
        TestRig rig = CreateRig(1);
        Assert.That(rig.Controller.TryBeginEncounter(), Is.True);
        rig.Behaviours[0].CompleteIntro();

        InvokePrivate(rig.Controller, "HandleHeroDeath");
        InvokePrivate(rig.Participants[0], "HandleDefeated");
        rig.Behaviours[0].CompleteDefeatPresentation();

        Assert.That(rig.Controller.State, Is.EqualTo(BossEncounterState.Interrupted));
        Assert.That(rig.Controller.CompletionCommitted, Is.False);
        Assert.That(registry.IsEncounterDefeated(definition.EncounterId), Is.False);
        Assert.That(rig.Reward.activeSelf, Is.False);
        Assert.That(rig.Barrier.IsOpen, Is.True);
        Assert.That(rig.ActorRoots[0].activeSelf, Is.False);
        Assert.That(rig.Behaviours[0].Interruptions, Is.EqualTo(1));
        Assert.That(rig.Behaviours[0].CompletionNotifications, Is.Zero);
    }

    [Test]
    public void HeroDeathAfterCommitCannotUndoCompletedState()
    {
        TestRig rig = CreateRig(1);
        Assert.That(rig.Controller.TryBeginEncounter(), Is.True);
        rig.Behaviours[0].CompleteIntro();
        InvokePrivate(rig.Participants[0], "HandleDefeated");
        rig.Behaviours[0].CompleteDefeatPresentation();

        InvokePrivate(rig.Controller, "HandleHeroDeath");
        InvokePrivate(rig.Controller, "CleanupForDisableOrDestroy");

        Assert.That(rig.Controller.State, Is.EqualTo(BossEncounterState.Completed));
        Assert.That(registry.IsEncounterDefeated(definition.EncounterId), Is.True);
        Assert.That(rig.Reward.activeSelf, Is.True);
        Assert.That(rig.Barrier.IsOpen, Is.True);
        Assert.That(rig.Behaviours[0].CompletionNotifications, Is.EqualTo(1));
        Assert.That(rig.Behaviours[0].Interruptions, Is.Zero);
    }

    [Test]
    public void AlreadyCompletedInitializationKeepsActorDormantAndReconcilesRoom()
    {
        registry.MarkEncounterDefeated(definition.EncounterId);
        TestRig rig = CreateRig(1);

        Assert.That(rig.Controller.State, Is.EqualTo(BossEncounterState.Completed));
        Assert.That(rig.Controller.CompletionCommitted, Is.True);
        Assert.That(rig.ActorRoots[0].activeSelf, Is.False);
        Assert.That(rig.Reward.activeSelf, Is.True);
        Assert.That(rig.Barrier.IsOpen, Is.True);
        Assert.That(rig.Controller.TryBeginEncounter(), Is.False);
        Assert.That(rig.Behaviours[0].PrepareCalls, Is.Zero);
    }

    [Test]
    public void FailedParticipantStartupReleasesArenaAndWritesNothing()
    {
        TestRig rig = CreateRig(1);
        SetPrivateField(rig.Participants[0], "health", null);

        LogAssert.Expect(LogType.Error, "[BossEncounterParticipant] 'Participant 0' is missing its actor root, health, or typed behaviour reference.");
        Assert.That(rig.Controller.TryBeginEncounter(), Is.False);

        Assert.That(rig.Controller.State, Is.EqualTo(BossEncounterState.Dormant));
        Assert.That(rig.Barrier.IsOpen, Is.True);
        Assert.That(rig.Reward.activeSelf, Is.False);
        Assert.That(registry.IsEncounterDefeated(definition.EncounterId), Is.False);
        Assert.That(rig.ActorRoots[0].activeSelf, Is.False);
    }

    private TestRig CreateRig(int participantCount)
    {
        HeroHealthComponent heroHealth = testRoot.AddComponent<HeroHealthComponent>();
        GameObject reward = new GameObject("Reward Root");
        reward.transform.SetParent(testRoot.transform);

        BossArenaBarrier barrier = CreateBarrier();
        BossEncounterParticipant[] participants = new BossEncounterParticipant[participantCount];
        BossEncounterTestBehaviour[] behaviours = new BossEncounterTestBehaviour[participantCount];
        GameObject[] actorRoots = new GameObject[participantCount];

        for (int i = 0; i < participantCount; i++)
        {
            GameObject wrapper = new GameObject($"Participant {i}");
            wrapper.transform.SetParent(testRoot.transform);
            BossEncounterParticipant participant = wrapper.AddComponent<BossEncounterParticipant>();

            GameObject actor = new GameObject("ActorRoot");
            actor.transform.SetParent(wrapper.transform);
            Rigidbody2D body = actor.AddComponent<Rigidbody2D>();
            EnemyStateBlackboard blackboard = actor.AddComponent<EnemyStateBlackboard>();
            EnemyHealthComponent health = actor.AddComponent<EnemyHealthComponent>();
            BossEncounterTestBehaviour behaviour = actor.AddComponent<BossEncounterTestBehaviour>();
            EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
            config.maxHealth = 2;
            health.Initialize(config, blackboard, body, null);
            behaviour.OwnedConfig = config;
            actor.SetActive(false);

            SetPrivateField(participant, "actorRoot", actor);
            SetPrivateField(participant, "health", health);
            SetPrivateField(participant, "behaviourSource", behaviour);
            participants[i] = participant;
            behaviours[i] = behaviour;
            actorRoots[i] = actor;
        }

        BossEncounterController controller = testRoot.AddComponent<BossEncounterController>();
        SetPrivateField(controller, "definition", definition);
        SetPrivateField(controller, "worldStateRegistry", registry);
        SetPrivateField(controller, "participants", participants);
        SetPrivateField(controller, "barriers", new[] { barrier });
        SetPrivateField(controller, "rewardRoot", reward);
        SetPrivateField(controller, "requiresTrigger", false);
        SetPrivateField(controller, "requiresBarriers", true);
        SetPrivateField(controller, "requiresCameraLock", false);
        SetPrivateField(controller, "lockHeroDuringIntro", false);
        SetPrivateField(controller, "lockHeroDuringOutro", false);
        SetPrivateField(controller, "heroHealth", heroHealth);

        testRoot.SetActive(true);
        InvokePrivate(controller, "InitializeEncounter");
        return new TestRig(controller, participants, behaviours, actorRoots, barrier, reward);
    }

    private BossArenaBarrier CreateBarrier()
    {
        GameObject barrierObject = new GameObject("Barrier");
        barrierObject.transform.SetParent(testRoot.transform);
        BoxCollider2D blocker = barrierObject.AddComponent<BoxCollider2D>();
        BossArenaBarrier barrier = barrierObject.AddComponent<BossArenaBarrier>();
        SetPrivateField(barrier, "blockerColliders", new Collider2D[] { blocker });
        return barrier;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field {target.GetType().Name}.{fieldName}");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method {target.GetType().Name}.{methodName}");
        method.Invoke(target, null);
    }

    private readonly struct TestRig
    {
        public TestRig(
            BossEncounterController controller,
            BossEncounterParticipant[] participants,
            BossEncounterTestBehaviour[] behaviours,
            GameObject[] actorRoots,
            BossArenaBarrier barrier,
            GameObject reward)
        {
            Controller = controller;
            Participants = participants;
            Behaviours = behaviours;
            ActorRoots = actorRoots;
            Barrier = barrier;
            Reward = reward;
        }

        public BossEncounterController Controller { get; }
        public BossEncounterParticipant[] Participants { get; }
        public BossEncounterTestBehaviour[] Behaviours { get; }
        public GameObject[] ActorRoots { get; }
        public BossArenaBarrier Barrier { get; }
        public GameObject Reward { get; }
    }
}

public sealed class BossEncounterTestBehaviour : MonoBehaviour, IBossEncounterBehaviour
{
    public event Action IntroCompleted;
    public event Action DefeatPresentationCompleted;

    public EnemyConfig OwnedConfig { get; set; }
    public int PrepareCalls { get; private set; }
    public int Interruptions { get; private set; }
    public int CompletionNotifications { get; private set; }

    public void PrepareForEncounter() => PrepareCalls++;
    public void PlayIntro() { }
    public void BeginCombat() { }
    public void InterruptEncounter() => Interruptions++;
    public void NotifyEncounterCompleted()
    {
        CompletionNotifications++;
        gameObject.SetActive(false);
    }
    public void CompleteIntro() => IntroCompleted?.Invoke();
    public void CompleteDefeatPresentation() => DefeatPresentationCompleted?.Invoke();

    private void OnDestroy()
    {
        if (OwnedConfig != null)
        {
            DestroyImmediate(OwnedConfig);
        }
    }
}
