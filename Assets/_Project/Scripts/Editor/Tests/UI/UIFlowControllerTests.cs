using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Package A1 coverage for the shared root/modal open-close flow, transition availability, and
/// the post-transition lockout. Exercises <see cref="UIFlowController"/>'s private state-machine
/// methods directly via reflection (the project's established test convention — see
/// HudDisplayTests/BossHealthDisplayTests) against a lightweight fake root, so it does not depend
/// on real Input System device polling or a full scene transition.
///
/// <see cref="GameManager.Awake"/> cannot be invoked directly in Edit Mode — it calls
/// <c>DontDestroyOnLoad</c>, which throws outside Play Mode. Since none of these tests need the
/// scene-load subscription Awake also sets up, <see cref="InitializeGameManagerForTest"/>
/// replicates only the two field assignments these tests actually depend on
/// (<c>_sceneLoader</c>/<c>_sceneTransitionManager</c>) and the static Instance, without running
/// Awake itself.
/// </summary>
public sealed class UIFlowControllerTests
{
    private sealed class FakeRootScreen : IUIFlowRootScreen
    {
        public event Action CloseRequested;
        public int ShowCount;
        public int HideCount;
        public bool HandleBackResult;
        public bool HasOpenModal { get; set; }
        public Selectable FirstSelection { get; set; }

        public void Show() => ShowCount++;
        public void Hide() => HideCount++;
        public bool HandleBackInternally() => HandleBackResult;
        public void RaiseCloseRequested() => CloseRequested?.Invoke();
    }

    private GameObject gameManagerObject;
    private GameManager gameManager;
    private GameObject flowObject;
    private UIFlowController flow;
    private GameObject eventSystemObject;
    private EventSystem eventSystem;
    private GameObject selectionObject;
    private Selectable firstSelectable;
    private FakeRootScreen pauseRoot;
    private FakeRootScreen gameplayMenuRoot;
    private PlayerHealthState healthState;

    [SetUp]
    public void SetUp()
    {
        ResetStaleGameManagerSingleton();

        gameManagerObject = new GameObject("GameManager Test");
        gameManager = gameManagerObject.AddComponent<GameManager>();
        InitializeGameManagerForTest(gameManager);

        eventSystemObject = new GameObject("EventSystem Test");
        eventSystem = eventSystemObject.AddComponent<EventSystem>();

        selectionObject = new GameObject("Selectable Test");
        firstSelectable = selectionObject.AddComponent<Button>();

        healthState = ScriptableObject.CreateInstance<PlayerHealthState>();
        healthState.SetMaximumHealth(5, restoreToFull: true);

        pauseRoot = new FakeRootScreen { FirstSelection = firstSelectable };
        gameplayMenuRoot = new FakeRootScreen { FirstSelection = firstSelectable };

        flowObject = new GameObject("UIFlowController Test");
        flow = flowObject.AddComponent<UIFlowController>();
        SetField(flow, "eventSystem", eventSystem);
        SetField(flow, "healthState", healthState);
        flow.ConfigureRoots(pauseRoot, null);

        ArmLockout(flow);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(flowObject);
        UnityEngine.Object.DestroyImmediate(gameManagerObject);
        UnityEngine.Object.DestroyImmediate(eventSystemObject);
        UnityEngine.Object.DestroyImmediate(selectionObject);
        UnityEngine.Object.DestroyImmediate(healthState);
        ResetStaleGameManagerSingleton();
    }

    /// <summary>
    /// Replicates only the two field assignments GameManager.Awake performs that
    /// UIFlowController actually reads (<c>_sceneLoader</c>, <c>_sceneTransitionManager</c>) plus
    /// the static Instance, without calling Awake itself (which calls DontDestroyOnLoad and
    /// throws in Edit Mode).
    /// </summary>
    private static void InitializeGameManagerForTest(GameManager gm)
    {
        SceneLoader sceneLoader = new SceneLoader();
        SceneTransitionManager transitionManager = new SceneTransitionManager(gm, sceneLoader);
        SetField(gm, "_sceneLoader", sceneLoader);
        SetField(gm, "_sceneTransitionManager", transitionManager);

        FieldInfo backing = typeof(GameManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(backing, Is.Not.Null, "GameManager.Instance backing field not found.");
        backing.SetValue(null, gm);
    }

    private static void ResetStaleGameManagerSingleton()
    {
        FieldInfo backing = typeof(GameManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        if (backing == null)
        {
            return;
        }

        GameManager stale = backing.GetValue(null) as GameManager;
        if (stale != null)
        {
            UnityEngine.Object.DestroyImmediate(stale.gameObject);
        }

        backing.SetValue(null, null);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo fi = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(fi, Is.Not.Null, "Field not found: " + name);
        fi.SetValue(target, value);
    }

    private static object GetField(object target, string name)
    {
        FieldInfo fi = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(fi, Is.Not.Null, "Field not found: " + name);
        return fi.GetValue(target);
    }

    private static void InvokePrivate(object target, string name)
    {
        MethodInfo mi = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(mi, Is.Not.Null, "Method not found: " + name);
        mi.Invoke(target, null);
    }

    /// <summary>Bypasses the boot-time "never armed until a transition has completed" lockout.</summary>
    private static void ArmLockout(UIFlowController flow)
    {
        SetField(flow, "lockoutRemaining", 0f);
    }

    private static void SetSceneTransitioning(GameManager gm, bool value)
    {
        object stm = GetField(gm, "_sceneTransitionManager");
        Assert.That(stm, Is.Not.Null);
        FieldInfo backing = stm.GetType().GetField("<IsTransitioning>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(backing, Is.Not.Null, "SceneTransitionManager.IsTransitioning backing field not found.");
        backing.SetValue(stm, value);
    }

    private void PressPause() => InvokePrivate(flow, "HandlePausePressed");
    private void PressGameplayMenu() => InvokePrivate(flow, "HandleGameplayMenuPressed");
    private void PressCancel() => InvokePrivate(flow, "HandleCancelPressed");

    [Test]
    public void PauseOpensRootAndAcquiresPauseExactlyOnce()
    {
        PressPause();

        Assert.That(flow.IsRootOpen, Is.True);
        Assert.That(flow.ActiveRootKind, Is.EqualTo(UIRootKind.Pause));
        Assert.That(pauseRoot.ShowCount, Is.EqualTo(1));
        Assert.That(gameManager.State, Is.EqualTo(GameState.Paused));
        Assert.That(Time.timeScale, Is.EqualTo(0f));

        Time.timeScale = 1f; // restore in case of assertion failure before Unpause runs
    }

    [Test]
    public void ValidFirstSelectionIsAppliedOnOpen()
    {
        PressPause();
        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(firstSelectable.gameObject));
        Time.timeScale = 1f;
    }

    [Test]
    public void PressingPauseAgainWhileStablyOpenClosesThroughSameSequenceExactlyOnce()
    {
        PressPause();
        PressPause();

        Assert.That(flow.IsRootOpen, Is.False);
        Assert.That(pauseRoot.HideCount, Is.EqualTo(1));
        Assert.That(gameManager.State, Is.EqualTo(GameState.Playing));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
    }

    [Test]
    public void ContinueEquivalentCloseRequestReleasesPauseExactlyOnce()
    {
        PressPause();
        pauseRoot.RaiseCloseRequested();

        Assert.That(flow.IsRootOpen, Is.False);
        Assert.That(pauseRoot.HideCount, Is.EqualTo(1));
        Assert.That(gameManager.State, Is.EqualTo(GameState.Playing));
    }

    [Test]
    public void RepeatedCallbacksDuringOpeningDoNothing()
    {
        SetField(flow, "state", Enum.Parse(GetField(flow, "state").GetType(), "Opening"));

        PressPause();

        Assert.That(pauseRoot.ShowCount, Is.EqualTo(0), "A request received while already opening must be ignored.");
        Assert.That(gameManager.State, Is.EqualTo(GameState.Playing), "Must not double-acquire pause.");
    }

    [Test]
    public void GameplayMenuRequestIsRejectedWhilePauseIsOpen()
    {
        flow.ConfigureRoots(pauseRoot, gameplayMenuRoot);
        PressPause();

        PressGameplayMenu();

        Assert.That(gameplayMenuRoot.ShowCount, Is.EqualTo(0));
        Assert.That(flow.ActiveRootKind, Is.EqualTo(UIRootKind.Pause), "The other root request must be rejected, not switch behind it.");

        Time.timeScale = 1f;
    }

    [Test]
    public void UnregisteredGameplayMenuRejectsSafelyWithNoPauseNoShowNoSelectionChange()
    {
        flow.ConfigureRoots(pauseRoot, null);

        PressGameplayMenu();

        Assert.That(flow.IsRootOpen, Is.False);
        Assert.That(gameManager.State, Is.EqualTo(GameState.Playing));
        Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
    }

    [Test]
    public void ModalOpenBlocksRootCloseUntilModalHandlesBackFirst()
    {
        PressPause();
        pauseRoot.HasOpenModal = true;
        pauseRoot.HandleBackResult = true;

        PressCancel();

        Assert.That(flow.IsRootOpen, Is.True, "Root must remain open; the fake root reported it handled Back internally (closed its own modal).");
        Assert.That(pauseRoot.HideCount, Is.EqualTo(0));

        Time.timeScale = 1f;
    }

    [Test]
    public void BackWithNoModalClosesTheRootThroughTheSameSequence()
    {
        PressPause();
        pauseRoot.HasOpenModal = false;
        pauseRoot.HandleBackResult = false;

        PressCancel();

        Assert.That(flow.IsRootOpen, Is.False);
        Assert.That(pauseRoot.HideCount, Is.EqualTo(1));
        Assert.That(gameManager.State, Is.EqualTo(GameState.Playing));
    }

    [Test]
    public void RequestIsRejectedWhileSceneTransitioning()
    {
        SetSceneTransitioning(gameManager, true);

        PressPause();

        Assert.That(flow.IsRootOpen, Is.False);
        Assert.That(gameManager.State, Is.EqualTo(GameState.Playing));

        SetSceneTransitioning(gameManager, false);
    }

    [Test]
    public void RequestIsRejectedWhileRespawnOrRecoveryInProgress()
    {
        SetField(gameManager, "_respawnOrRecoveryInProgress", true);

        PressPause();

        Assert.That(flow.IsRootOpen, Is.False);

        SetField(gameManager, "_respawnOrRecoveryInProgress", false);
    }

    [Test]
    public void RequestIsRejectedWhileHealthIsDepleted()
    {
        healthState.ForceDeplete();

        PressPause();

        Assert.That(flow.IsRootOpen, Is.False);
    }

    [Test]
    public void TransitionFallingEdgeStartsOneSecondUnscaledLockoutAndBlocksUntilElapsed()
    {
        SetField(flow, "lockoutRemaining", float.MaxValue);
        SetField(flow, "wasTransitioning", false);

        SetSceneTransitioning(gameManager, true);
        InvokePrivate(flow, "UpdateTransitionLockout");
        Assert.That(GetField(flow, "wasTransitioning"), Is.EqualTo(true));

        SetSceneTransitioning(gameManager, false);
        InvokePrivate(flow, "UpdateTransitionLockout");

        // Time.unscaledDeltaTime is not driven by a running player loop between synchronous
        // EditMode calls, so assert the armed range rather than an exact post-decrement value.
        float lockoutAfterEdge = (float)GetField(flow, "lockoutRemaining");
        Assert.That(lockoutAfterEdge, Is.GreaterThan(0f).And.LessThanOrEqualTo(1.0f),
            "Falling edge while Playing must start the configured 1.0s unscaled lockout.");

        PressPause();
        Assert.That(flow.IsRootOpen, Is.False, "Requests during the lockout must still be rejected.");

        SetField(flow, "lockoutRemaining", 0f);
        PressPause();
        Assert.That(flow.IsRootOpen, Is.True, "Once elapsed, a fresh press must open normally.");

        Time.timeScale = 1f;
    }

    [Test]
    public void BlockedRequestsCreateNoPendingOrQueuedState()
    {
        SetSceneTransitioning(gameManager, true);
        PressPause();
        SetSceneTransitioning(gameManager, false);
        SetField(flow, "lockoutRemaining", 0f);

        // No further press occurs; a blocked request must not open later on its own.
        Assert.That(flow.IsRootOpen, Is.False);
        Assert.That(pauseRoot.ShowCount, Is.EqualTo(0));
    }
}
