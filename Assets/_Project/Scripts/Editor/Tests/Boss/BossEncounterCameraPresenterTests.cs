using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Camera Phase 3 boss integration: the scene-side presentation adapter follows the existing
// encounter lifecycle without acquiring any authority over it.
public sealed class BossEncounterCameraPresenterTests
{
    private GameObject testRoot;
    private GameObject cameraRoot;
    private GameObject controllerObject;
    private GameObject targetObject;
    private GameObject heroObject;
    private GameObject bossFocusObject;
    private GameObject rewardFocusObject;

    private BossEncounterDefinition definition;
    private WorldStateRegistry registry;
    private GameCameras cameras;
    private CameraController controller;
    private CameraTarget cameraTarget;

    [SetUp]
    public void SetUp()
    {
        heroObject = new GameObject("Camera Test Hero") { tag = "Player" };
        heroObject.AddComponent<BoxCollider2D>();

        targetObject = new GameObject("Camera Target");
        cameraTarget = targetObject.AddComponent<CameraTarget>();

        controllerObject = new GameObject("Camera Controller");
        controllerObject.AddComponent<Camera>();
        controller = controllerObject.AddComponent<CameraController>();
        SetPrivateField(controller, "cameraTarget", cameraTarget);
        InvokePrivate(controller, "Awake");
        cameraTarget.SceneInit();
        controller.SceneInit();
        SetPrivateField(controller, "startTimer", 0f);

        cameraRoot = new GameObject("Game Cameras");
        cameras = cameraRoot.AddComponent<GameCameras>();
        SetPrivateField(cameras, "cameraTarget", cameraTarget);
        SetPrivateField(cameras, "cameraController", controller);
        EnsureCamerasInstance();

        // Routes the encounter's CameraEventService lock-entered request into the controller,
        // exactly as the persistent prefab does at runtime.
        InvokePrivate(cameras, "OnEnable");

        testRoot = new GameObject("Boss Presenter Test");
        testRoot.SetActive(false);
        definition = ScriptableObject.CreateInstance<BossEncounterDefinition>();
        registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        SetPrivateField(definition, "encounterId", "test_boss_presenter");

        bossFocusObject = new GameObject("Boss Focus");
        bossFocusObject.transform.position = new Vector3(6f, 1f, 0f);
        rewardFocusObject = new GameObject("Reward Focus");
        rewardFocusObject.transform.position = new Vector3(-4f, 0f, 0f);
    }

    [TearDown]
    public void TearDown()
    {
        if (testRoot != null) UnityEngine.Object.DestroyImmediate(testRoot);
        UnityEngine.Object.DestroyImmediate(definition);
        UnityEngine.Object.DestroyImmediate(registry);
        UnityEngine.Object.DestroyImmediate(bossFocusObject);
        UnityEngine.Object.DestroyImmediate(rewardFocusObject);

        if (cameras != null)
        {
            InvokePrivate(cameras, "OnDisable");
        }

        if (GameCameras.Instance != null)
        {
            UnityEngine.Object.DestroyImmediate(GameCameras.Instance.gameObject);
        }

        if (cameraRoot != null) UnityEngine.Object.DestroyImmediate(cameraRoot);
        UnityEngine.Object.DestroyImmediate(controllerObject);
        UnityEngine.Object.DestroyImmediate(targetObject);
        UnityEngine.Object.DestroyImmediate(heroObject);
    }

    [Test]
    public void EncounterStartAcquiresTheIntroPresentation()
    {
        Rig rig = CreateRig();

        Assert.That(rig.Controller.TryBeginEncounter(), Is.True);

        Assert.That(rig.Presenter.HasIntroPresentation, Is.True);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));
        Assert.That(controller.HasPresentationRequest, Is.True);
        Assert.That(controller.PresentationSettings.mode, Is.EqualTo(CameraPresentationMode.FocusTarget));
    }

    [Test]
    public void EncounterActivationReplacesIntroWithHeroAndBossFraming()
    {
        Rig rig = CreateRig();
        Assert.That(rig.Controller.TryBeginEncounter(), Is.True);

        rig.Behaviour.CompleteIntro();

        Assert.That(rig.Controller.State, Is.EqualTo(BossEncounterState.Active));
        Assert.That(rig.Presenter.HasIntroPresentation, Is.False);
        Assert.That(rig.Presenter.HasCombatPresentation, Is.True);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));
        Assert.That(controller.PresentationSettings.mode, Is.EqualTo(CameraPresentationMode.FrameTargets));

        InvokePrivate(controller, "ResolvePresentationFraming");
        Assert.That(controller.PresentationFraming.ValidTargetCount, Is.EqualTo(2),
            "Combat framing should include both the hero and the boss.");
    }

    [Test]
    public void PhaseChangeAddsAHigherPriorityFocusThatFallsBackToCombatFraming()
    {
        Rig rig = CreateRig(withPhaseSource: true);
        Assert.That(rig.Controller.TryBeginEncounter(), Is.True);
        rig.Behaviour.CompleteIntro();
        Assert.That(rig.Presenter.HasCombatPresentation, Is.True);

        rig.PhaseSource.RaisePhase(2);

        Assert.That(rig.Presenter.HasPhasePresentation, Is.True);
        Assert.That(rig.Presenter.HasCombatPresentation, Is.True, "Combat framing must stay registered underneath.");
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(2));
        Assert.That(controller.PresentationSettings.mode, Is.EqualTo(CameraPresentationMode.FocusTarget));

        // Expiring the timed phase focus must return selection to the still-registered combat request.
        ExpireAllTimedRequests();
        InvokePrivate(cameras, "Update");

        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));
        Assert.That(controller.PresentationSettings.mode, Is.EqualTo(CameraPresentationMode.FrameTargets));
    }

    [Test]
    public void BossDefeatReplacesCombatFramingWithADefeatFocus()
    {
        Rig rig = CreateRig();
        Assert.That(rig.Controller.TryBeginEncounter(), Is.True);
        rig.Behaviour.CompleteIntro();

        InvokePrivate(rig.Participant, "HandleDefeated");

        Assert.That(rig.Controller.State, Is.EqualTo(BossEncounterState.BossesDefeated));
        Assert.That(rig.Presenter.HasDefeatPresentation, Is.True);
        Assert.That(rig.Presenter.HasCombatPresentation, Is.False);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));
    }

    [Test]
    public void CompletionReleasesEveryCombatHandleAndRevealsTheReward()
    {
        Rig rig = CreateRig();
        Assert.That(rig.Controller.TryBeginEncounter(), Is.True);
        rig.Behaviour.CompleteIntro();
        InvokePrivate(rig.Participant, "HandleDefeated");
        rig.Behaviour.CompleteDefeatPresentation();

        Assert.That(rig.Controller.State, Is.EqualTo(BossEncounterState.Completed));
        Assert.That(rig.Presenter.HasIntroPresentation, Is.False);
        Assert.That(rig.Presenter.HasCombatPresentation, Is.False);
        Assert.That(rig.Presenter.HasPhasePresentation, Is.False);
        Assert.That(rig.Presenter.HasDefeatPresentation, Is.False);
        Assert.That(rig.Presenter.HasRewardPresentation, Is.True);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));

        // The reward reveal is self-expiring: nothing must remain afterwards.
        ExpireAllTimedRequests();
        InvokePrivate(cameras, "Update");

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
        Assert.That(controller.HasPresentationRequest, Is.False);
    }

    [Test]
    public void HeroDeathReleasesEveryPresentationHandle()
    {
        Rig rig = CreateRig(withPhaseSource: true);
        Assert.That(rig.Controller.TryBeginEncounter(), Is.True);
        rig.Behaviour.CompleteIntro();
        rig.PhaseSource.RaisePhase(2);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(2));

        InvokePrivate(rig.Controller, "HandleHeroDeath");

        Assert.That(rig.Controller.State, Is.EqualTo(BossEncounterState.Interrupted));
        Assert.That(cameras.ActivePresentationCount, Is.Zero);
        Assert.That(controller.HasPresentationRequest, Is.False);
    }

    [Test]
    public void ComponentDisableReleasesEveryPresentationHandle()
    {
        Rig rig = CreateRig();
        Assert.That(rig.Controller.TryBeginEncounter(), Is.True);
        rig.Behaviour.CompleteIntro();
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));

        InvokePrivate(rig.Presenter, "OnDisable");

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
        Assert.That(controller.HasPresentationRequest, Is.False);
    }

    [Test]
    public void EncounterUnloadCleanupReleasesEveryPresentationHandle()
    {
        Rig rig = CreateRig();
        Assert.That(rig.Controller.TryBeginEncounter(), Is.True);
        rig.Behaviour.CompleteIntro();

        InvokePrivate(rig.Controller, "CleanupForDisableOrDestroy");

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
    }

    [Test]
    public void PresentationDoesNotChangePersistenceOrBlockParticipantCompletion()
    {
        Rig rig = CreateRig(withPhaseSource: true);
        Assert.That(rig.Controller.TryBeginEncounter(), Is.True);
        Assert.That(registry.IsEncounterDefeated(definition.EncounterId), Is.False);

        rig.Behaviour.CompleteIntro();
        rig.PhaseSource.RaisePhase(2);
        Assert.That(registry.IsEncounterDefeated(definition.EncounterId), Is.False);

        InvokePrivate(rig.Participant, "HandleDefeated");
        Assert.That(registry.IsEncounterDefeated(definition.EncounterId), Is.False,
            "Presentation must not commit persistence before the presentation gate closes.");

        rig.Behaviour.CompleteDefeatPresentation();

        Assert.That(rig.Controller.CompletionCommitted, Is.True);
        Assert.That(registry.IsEncounterDefeated(definition.EncounterId), Is.True);
        Assert.That(rig.Behaviour.CompletionNotifications, Is.EqualTo(1));
        Assert.That(rig.Reward.activeSelf, Is.True);
    }

    [Test]
    public void PresentationLeavesTheArenaCameraLockAuthoritative()
    {
        Rig rig = CreateRig(withCameraLock: true);
        Assert.That(rig.Controller.TryBeginEncounter(), Is.True);
        rig.Behaviour.CompleteIntro();

        Assert.That(controller.CurrentLockArea, Is.SameAs(rig.CameraLock),
            "The encounter's own lock must remain the selected underlying lock while presenting.");
        Assert.That(controller.HasPresentationRequest, Is.True);
    }

    [Test]
    public void FailedEncounterStartLeavesNoPresentationHandles()
    {
        Rig rig = CreateRig();
        rig.Behaviour.PreparationSucceeds = false;
        UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

        try
        {
            Assert.That(rig.Controller.TryBeginEncounter(), Is.False);
        }
        finally
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
        Assert.That(controller.HasPresentationRequest, Is.False);
    }

    // --------------------------------------------------------------------------------- helpers

    private sealed class Rig
    {
        public BossEncounterController Controller;
        public BossEncounterCameraPresenter Presenter;
        public BossEncounterParticipant Participant;
        public BossEncounterTestBehaviour Behaviour;
        public PresentationPhaseSourceDouble PhaseSource;
        public CameraLockArea CameraLock;
        public GameObject Reward;
    }

    private Rig CreateRig(bool withPhaseSource = false, bool withCameraLock = false)
    {
        HeroHealthComponent heroHealth = testRoot.AddComponent<HeroHealthComponent>();

        GameObject reward = new GameObject("Reward Root");
        reward.transform.SetParent(testRoot.transform);

        GameObject barrierObject = new GameObject("Barrier");
        barrierObject.transform.SetParent(testRoot.transform);
        BoxCollider2D blocker = barrierObject.AddComponent<BoxCollider2D>();
        BossArenaBarrier barrier = barrierObject.AddComponent<BossArenaBarrier>();
        SetPrivateField(barrier, "blockerColliders", new Collider2D[] { blocker });

        GameObject wrapper = new GameObject("Participant");
        wrapper.transform.SetParent(testRoot.transform);
        BossEncounterParticipant participant = wrapper.AddComponent<BossEncounterParticipant>();

        GameObject actor = new GameObject("ActorRoot");
        actor.transform.SetParent(wrapper.transform);
        Rigidbody2D body = actor.AddComponent<Rigidbody2D>();
        actor.AddComponent<BoxCollider2D>();
        actor.AddComponent<SpriteRenderer>();
        EnemyStateBlackboard blackboard = actor.AddComponent<EnemyStateBlackboard>();
        EnemyHealthComponent health = actor.AddComponent<EnemyHealthComponent>();
        BossEncounterTestBehaviour behaviour = actor.AddComponent<BossEncounterTestBehaviour>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        config.maxHealth = 2;
        health.Initialize(config, blackboard, body, null);
        behaviour.OwnedConfig = config;
        behaviour.OwnerParticipant = participant;
        actor.SetActive(false);

        SetPrivateField(participant, "actorRoot", actor);
        SetPrivateField(participant, "health", health);
        SetPrivateField(participant, "behaviourSource", behaviour);

        CameraLockArea cameraLock = null;
        if (withCameraLock)
        {
            GameObject lockObject = new GameObject("Arena Camera Lock");
            lockObject.transform.SetParent(testRoot.transform);
            BoxCollider2D lockCollider = lockObject.AddComponent<BoxCollider2D>();
            lockCollider.isTrigger = true;
            lockCollider.size = new Vector2(40f, 30f);
            cameraLock = lockObject.AddComponent<CameraLockArea>();
            SetPrivateField(cameraLock, "priority", 50);
        }

        BossEncounterController controllerComponent = testRoot.AddComponent<BossEncounterController>();
        SetPrivateField(controllerComponent, "definition", definition);
        SetPrivateField(controllerComponent, "worldStateRegistry", registry);
        SetPrivateField(controllerComponent, "participants", new[] { participant });
        SetPrivateField(controllerComponent, "barriers", new[] { barrier });
        SetPrivateField(controllerComponent, "rewardRoot", reward);
        SetPrivateField(controllerComponent, "requiresTrigger", false);
        SetPrivateField(controllerComponent, "requiresBarriers", true);
        SetPrivateField(controllerComponent, "requiresCameraLock", withCameraLock);
        SetPrivateField(controllerComponent, "cameraLockArea", cameraLock);
        SetPrivateField(controllerComponent, "lockHeroDuringIntro", false);
        SetPrivateField(controllerComponent, "lockHeroDuringOutro", false);
        SetPrivateField(controllerComponent, "heroHealth", heroHealth);

        PresentationPhaseSourceDouble phaseSource = null;
        if (withPhaseSource)
        {
            phaseSource = testRoot.AddComponent<PresentationPhaseSourceDouble>();
        }

        BossEncounterCameraPresenter presenter = testRoot.AddComponent<BossEncounterCameraPresenter>();
        SetPrivateField(presenter, "encounter", controllerComponent);
        SetPrivateField(presenter, "phaseSource", phaseSource);
        SetPrivateField(presenter, "bossFocus", bossFocusObject.transform);
        SetPrivateField(presenter, "rewardFocus", rewardFocusObject.transform);
        SetPrivateField(presenter, "introDirector", null);
        SetPrivateField(presenter, "phaseFocusDuration", 0.0001f);
        SetPrivateField(presenter, "rewardRevealDuration", 0.0001f);

        testRoot.SetActive(true);
        InvokePrivate(controllerComponent, "InitializeEncounter");
        InvokePrivate(presenter, "Awake");
        InvokePrivate(presenter, "OnEnable");

        return new Rig
        {
            Controller = controllerComponent,
            Presenter = presenter,
            Participant = participant,
            Behaviour = behaviour,
            PhaseSource = phaseSource,
            CameraLock = cameraLock,
            Reward = reward
        };
    }

    // Timed requests use realtimeSinceStartup; the rig authors sub-millisecond durations so a
    // short sleep is enough to make expiry deterministic without waiting on frames.
    private static void ExpireAllTimedRequests()
    {
        System.Threading.Thread.Sleep(5);
    }

    private void EnsureCamerasInstance()
    {
        if (GameCameras.Instance == cameras)
        {
            return;
        }

        if (GameCameras.Instance != null && GameCameras.Instance != cameras)
        {
            UnityEngine.Object.DestroyImmediate(GameCameras.Instance.gameObject);
        }

        typeof(GameCameras)
            .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, cameras);
    }

    private static void SetPrivateField(object target, string field, object value)
    {
        target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SetValue(target, value);
    }

    private static object InvokePrivate(object target, string method, params object[] arguments)
    {
        foreach (MethodInfo candidate in target.GetType().GetMethods(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (candidate.Name == method && candidate.GetParameters().Length == arguments.Length)
            {
                return candidate.Invoke(target, arguments);
            }
        }

        Assert.Fail($"Method '{method}' was not found on {target.GetType().Name}.");
        return null;
    }
}

// Minimal neutral phase source, standing in for a concrete boss actor.
public sealed class PresentationPhaseSourceDouble : MonoBehaviour, IBossPresentationPhaseSource
{
    public event Action<int> PresentationPhaseChanged;

    public int CurrentPresentationPhase { get; private set; } = 1;

    public void RaisePhase(int phase)
    {
        CurrentPresentationPhase = phase;
        PresentationPhaseChanged?.Invoke(phase);
    }
}
