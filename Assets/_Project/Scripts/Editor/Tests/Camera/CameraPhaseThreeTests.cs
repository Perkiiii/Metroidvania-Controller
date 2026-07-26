using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

// Camera Phase 3: presentation-request ownership, camera-layer interaction, framing, and zoom.
public sealed class CameraPhaseThreeTests
{
    private const float BaseHalfHeight = 8.098405f;
    private const float Aspect = 16f / 9f;
    private static readonly float BaseHalfWidth = BaseHalfHeight * Aspect;

    private GameObject hero;
    private GameObject targetObject;
    private GameObject controllerObject;
    private GameObject camerasObject;
    private CameraTarget target;
    private CameraController controller;
    private GameCameras cameras;
    private Camera cam;
    private readonly List<GameObject> temporaries = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        hero = new GameObject("Camera Test Hero") { tag = "Player" };
        hero.AddComponent<BoxCollider2D>();

        targetObject = new GameObject("Camera Target");
        target = targetObject.AddComponent<CameraTarget>();

        controllerObject = new GameObject("Camera Controller");
        cam = controllerObject.AddComponent<Camera>();
        controller = controllerObject.AddComponent<CameraController>();
        SetField(controller, "cameraTarget", target);
        Invoke(controller, "Awake");

        // Deterministic projection: framing and zoom assertions depend on the viewport, and the
        // Game view aspect must not leak into EditMode results.
        cam.aspect = Aspect;

        // Zero the target's vertical framing offset at the source so SnapToHero cannot reinstate
        // it midway through a test.
        SetField(target, "baseVerticalOffset", 0f);
        target.SceneInit();
        controller.SceneInit();
        SetField(target, "baseVerticalOffset", 0f);
        SetField(target, "currentVerticalOffset", 0f);
        SetField(controller, "startTimer", 0f);

        // Per-request padding is additive to the shared CameraConfig padding. Zeroing the shared
        // side keeps framing assertions expressed purely in per-request terms; the additive rule
        // itself is covered by SharedPaddingAddsToRequestPadding.
        SetField(controller, "presentationPaddingX", 0f);
        SetField(controller, "presentationPaddingY", 0f);

        camerasObject = new GameObject("Game Cameras");
        cameras = camerasObject.AddComponent<GameCameras>();
        SetField(cameras, "cameraTarget", target);
        SetField(cameras, "cameraController", controller);
        EnsureCamerasInstance();
        Invoke(cameras, "OnEnable");
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < temporaries.Count; i++)
        {
            if (temporaries[i] != null)
            {
                Object.DestroyImmediate(temporaries[i]);
            }
        }

        temporaries.Clear();

        if (cameras != null)
        {
            // Undo the static CameraEventService subscriptions installed in SetUp.
            Invoke(cameras, "OnDisable");
        }

        if (GameCameras.Instance != null)
        {
            Object.DestroyImmediate(GameCameras.Instance.gameObject);
        }

        if (camerasObject != null)
        {
            Object.DestroyImmediate(camerasObject);
        }

        Object.DestroyImmediate(controllerObject);
        Object.DestroyImmediate(targetObject);
        Object.DestroyImmediate(hero);
    }

    // --------------------------------------------------------------------- request ownership

    [Test]
    public void AcquireRegistersAndSelectsRequest()
    {
        Transform focus = CreateTarget("Focus", new Vector3(20f, 5f, 0f));
        CameraPresentationHandle handle = AcquireFocusTarget(focus, priority: 0);

        Assert.That(handle.IsValid, Is.True);
        Assert.That(handle.IsRegistered, Is.True);
        Assert.That(handle.IsSelected, Is.True);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));
        Assert.That(controller.HasPresentationRequest, Is.True);
        Assert.That(controller.PresentationRequestId, Is.EqualTo(cameras.SelectedPresentationId));
    }

    [Test]
    public void ReleaseRemovesRequestAndClearsController()
    {
        CameraPresentationHandle handle = AcquireFocusTarget(CreateTarget("Focus", Vector3.zero), 0);

        handle.Release();

        Assert.That(handle.IsRegistered, Is.False);
        Assert.That(cameras.ActivePresentationCount, Is.Zero);
        Assert.That(controller.HasPresentationRequest, Is.False);
        Assert.That(cameras.SelectedPresentationId, Is.Zero);
    }

    [Test]
    public void DuplicateReleaseIsSafe()
    {
        CameraPresentationHandle handle = AcquireFocusTarget(CreateTarget("Focus", Vector3.zero), 0);

        handle.Release();
        Assert.DoesNotThrow(() => handle.Release());
        Assert.DoesNotThrow(() => handle.Dispose());

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
    }

    [Test]
    public void ReleasingOneHandleDoesNotReleaseAnother()
    {
        CameraPresentationHandle first = AcquireFocusTarget(CreateTarget("A", Vector3.zero), 0);
        CameraPresentationHandle second = AcquireFocusTarget(CreateTarget("B", Vector3.one), 0);

        first.Release();

        Assert.That(first.IsRegistered, Is.False);
        Assert.That(second.IsRegistered, Is.True);
        Assert.That(second.IsSelected, Is.True);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));
    }

    [Test]
    public void HighestPriorityRequestIsSelectedRegardlessOfOrder()
    {
        CameraPresentationHandle high = AcquireFocusTarget(CreateTarget("High", Vector3.zero), priority: 10);
        CameraPresentationHandle low = AcquireFocusTarget(CreateTarget("Low", Vector3.one), priority: 1);

        Assert.That(high.IsSelected, Is.True);
        Assert.That(low.IsSelected, Is.False);
    }

    [Test]
    public void NewestRequestWinsEqualPriority()
    {
        CameraPresentationHandle older = AcquireFocusTarget(CreateTarget("Older", Vector3.zero), priority: 5);
        CameraPresentationHandle newer = AcquireFocusTarget(CreateTarget("Newer", Vector3.one), priority: 5);

        Assert.That(newer.IsSelected, Is.True);
        Assert.That(older.IsSelected, Is.False);
    }

    [Test]
    public void RemovingNonSelectedRequestDoesNotDisturbSelection()
    {
        CameraPresentationHandle selected = AcquireFocusTarget(CreateTarget("Selected", Vector3.zero), priority: 10);
        CameraPresentationHandle other = AcquireFocusTarget(CreateTarget("Other", Vector3.one), priority: 1);
        long selectedId = cameras.SelectedPresentationId;
        CameraTransitionCause causeBefore = controller.CurrentTransitionCause;

        other.Release();

        Assert.That(cameras.SelectedPresentationId, Is.EqualTo(selectedId));
        Assert.That(selected.IsSelected, Is.True);
        Assert.That(controller.CurrentTransitionCause, Is.EqualTo(causeBefore));
    }

    [Test]
    public void RemovingSelectedRequestFallsBackToNextBest()
    {
        CameraPresentationHandle fallback = AcquireFocusTarget(CreateTarget("Fallback", Vector3.zero), priority: 1);
        CameraPresentationHandle selected = AcquireFocusTarget(CreateTarget("Selected", Vector3.one), priority: 10);

        selected.Release();

        Assert.That(fallback.IsSelected, Is.True);
        Assert.That(controller.HasPresentationRequest, Is.True);
        Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.PresentationChanged));
    }

    [Test]
    public void DestroyedUnitySourceIsPruned()
    {
        GameObject source = new GameObject("Presentation Source");
        temporaries.Add(source);
        Transform focus = CreateTarget("Focus", Vector3.zero);
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
        cameras.AcquirePresentation(settings, new[] { focus }, source, CameraRequestLifetime.Scene);

        Object.DestroyImmediate(source);
        Invoke(cameras, "Update");

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
        Assert.That(controller.HasPresentationRequest, Is.False);
    }

    [Test]
    public void DestroyedTargetPrunesRequestAndRestoresUnderlyingFraming()
    {
        Transform focus = CreateTarget("Focus", new Vector3(30f, 0f, 0f));
        AcquireFocusTarget(focus, 0);
        Object.DestroyImmediate(focus.gameObject);

        Invoke(cameras, "Update");

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
        Assert.That(controller.HasPresentationRequest, Is.False);

        SnapCamera();
        Assert.That(controller.RenderedPosition.x, Is.EqualTo(hero.transform.position.x).Within(0.001f));
    }

    [Test]
    public void SceneLifetimeRequestIsClearedOnSceneUnload()
    {
        AcquireFocusTarget(CreateTarget("Focus", Vector3.zero), 0);

        Invoke(cameras, "OnSceneUnloaded", SceneManager.GetActiveScene());

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
        Assert.That(controller.HasPresentationRequest, Is.False);
    }

    [Test]
    public void PersistentRequestSurvivesSceneUnload()
    {
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
        CameraPresentationHandle handle = cameras.AcquirePresentation(
            settings,
            new[] { CreateTarget("Focus", Vector3.zero) },
            this,
            CameraRequestLifetime.Persistent);

        Invoke(cameras, "OnSceneUnloaded", SceneManager.GetActiveScene());

        Assert.That(handle.IsRegistered, Is.True);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));
    }

    [Test]
    public void TimedRequestExpiresWithoutAffectingOthers()
    {
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
        CameraPresentationHandle indefinite = cameras.AcquirePresentation(
            settings, new[] { CreateTarget("Indefinite", Vector3.zero) }, this, CameraRequestLifetime.Scene, 1);
        CameraPresentationHandle timed = cameras.AcquirePresentation(
            settings, new[] { CreateTarget("Timed", Vector3.one) }, this, CameraRequestLifetime.Scene, 5, 0.0001f);

        Assert.That(timed.IsSelected, Is.True);

        System.Threading.Thread.Sleep(5);
        Invoke(cameras, "Update");

        Assert.That(timed.IsRegistered, Is.False);
        Assert.That(indefinite.IsRegistered, Is.True);
        Assert.That(indefinite.IsSelected, Is.True);
    }

    // ---------------------------------------------------------------- camera-layer interaction

    [Test]
    public void UnderlyingLockSelectionContinuesWhilePresentationIsActive()
    {
        AcquireFocusTarget(CreateTarget("Focus", Vector3.zero), 0);
        CameraLockArea area = CreateLock("Area", 1);

        controller.EnterLockArea(area);

        Assert.That(controller.CurrentLockArea, Is.SameAs(area));
        Assert.That(controller.HasPresentationRequest, Is.True);
    }

    [Test]
    public void UnderlyingBoundsChangesContinueWhilePresentationIsActive()
    {
        AcquireFocusTarget(CreateTarget("Focus", Vector3.zero), 0);
        CameraBoundsVolume volume = CreateBounds("Room", new Vector2(200f, 200f), Vector2.zero);

        controller.SetBoundsVolume(volume);

        Assert.That(controller.CurrentBoundsVolume, Is.SameAs(volume));
        Assert.That(controller.ActiveBoundsCount, Is.EqualTo(1));
    }

    [Test]
    public void ReleaseReturnsToTheCurrentUnderlyingLockNotAStaleOne()
    {
        CameraLockArea first = CreateLock("First", 1);
        CameraLockArea second = CreateLock("Second", 2);
        controller.EnterLockArea(first);

        CameraPresentationHandle handle = AcquireFocusTarget(CreateTarget("Focus", new Vector3(40f, 10f, 0f)), 0);

        // The underlying lock changes underneath the active presentation.
        controller.ExitLockArea(first);
        controller.EnterLockArea(second);
        Assert.That(controller.CurrentLockArea, Is.SameAs(second));

        handle.Release();

        Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.PresentationReleased));
        Assert.That(controller.TransitionDestinationLockArea, Is.SameAs(second));

        SnapCamera();
        Assert.That(controller.RenderedPosition.x, Is.EqualTo(hero.transform.position.x).Within(0.001f));
    }

    [Test]
    public void HardFreezeSuppressesLivePresentationMovementAndZoom()
    {
        controller.ApplyFreezeState(true, true);
        Vector3 before = controller.RenderedPosition;
        float fovBefore = cam.fieldOfView;

        AcquireFocusTarget(CreateTarget("Focus", new Vector3(50f, 20f, 0f)), 0);
        Invoke(controller, "LateUpdate");

        Assert.That(controller.Mode, Is.EqualTo(CameraMode.Frozen));
        Assert.That(controller.RenderedPosition, Is.EqualTo(before));
        Assert.That(cam.fieldOfView, Is.EqualTo(fovBefore).Within(0.0001f));
        Assert.That(controller.IsTransitioning, Is.False);
    }

    [Test]
    public void FreezeReleaseResumesTheStillSelectedPresentation()
    {
        AcquireFocusTarget(CreateTarget("Focus", new Vector3(25f, 6f, 0f)), 0);
        controller.ApplyFreezeState(true, true);

        controller.ApplyFreezeState(false, false);
        ResolveFraming();
        SnapCamera();

        Assert.That(controller.HasPresentationRequest, Is.True);
        Assert.That(controller.RenderedPosition.x, Is.EqualTo(25f).Within(0.001f));
        Assert.That(controller.RenderedPosition.y, Is.EqualTo(6f).Within(0.001f));
    }

    [Test]
    public void FreeModeIsNotSilentlyReplacedByAPresentationRequest()
    {
        controller.StartFreeMode();

        AcquireFocusTarget(CreateTarget("Focus", new Vector3(12f, 0f, 0f)), 0);

        Assert.That(controller.Mode, Is.EqualTo(CameraMode.Free));
        Assert.That(controller.IsTransitioning, Is.False);
        Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.None));
    }

    [Test]
    public void SceneEntryImmediateFramesUnderlyingDestinationNotThePresentation()
    {
        hero.transform.position = new Vector3(9f, -2f, 0f);
        target.SceneInit();
        SetField(target, "currentVerticalOffset", 0f);
        AcquireFocusTarget(CreateTarget("Focus", new Vector3(80f, 40f, 0f)), 0);
        Assert.That(controller.HasPresentationRequest, Is.True);

        Invoke(cameras, "ResetPresentationForSceneEntry");
        target.SnapToHero();
        cam.aspect = Aspect;
        bool applied = controller.ApplySceneEntryImmediate(out string failureDetail);

        Assert.That(applied, Is.True, failureDetail);
        Assert.That(controller.HasPresentationRequest, Is.False);
        Assert.That(controller.RenderedPosition.x, Is.EqualTo(9f).Within(0.001f));
        Assert.That(controller.RenderedPosition.y, Is.EqualTo(-2f).Within(0.001f));
        Assert.That(controller.CurrentZoom, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(controller.IsTransitioning, Is.False);
    }

    [Test]
    public void SceneInitClearsPresentationStateAndZoom()
    {
        AcquireFocusTarget(CreateTarget("Focus", new Vector3(30f, 0f, 0f)), 0);
        ResolveFraming();
        SnapCamera();

        controller.SceneInit();

        Assert.That(controller.HasPresentationRequest, Is.False);
        Assert.That(controller.CurrentZoom, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(cam.fieldOfView, Is.EqualTo(24f).Within(0.001f));
    }

    [Test]
    public void PresentationUsesItsOwnTransitionCauses()
    {
        CameraPresentationHandle first = AcquireFocusTarget(CreateTarget("First", Vector3.zero), 1);
        Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.PresentationEntered));

        CameraPresentationHandle second = AcquireFocusTarget(CreateTarget("Second", Vector3.one), 5);
        Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.PresentationChanged));

        second.Release();
        first.Release();
        Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.PresentationReleased));
    }

    [Test]
    public void PerRequestBlendOverrideWinsOverSharedDefault()
    {
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
        settings.overrideBlend = true;
        settings.blendIn = CameraTransitionSettings.Live(0.42f, 0.9f, true);
        cameras.AcquirePresentation(settings, new[] { CreateTarget("Focus", Vector3.zero) }, this);

        Assert.That(controller.CurrentDampTimeX, Is.EqualTo(0.42f).Within(0.0001f));
        Assert.That(controller.TransitionDuration, Is.EqualTo(0.9f).Within(0.0001f));
    }

    // ---------------------------------------------------------------------- framing and zoom

    [Test]
    public void FocusTargetFramesTheTargetPlusOffset()
    {
        Transform focus = CreateTarget("Focus", new Vector3(14f, 3f, 0f));
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
        settings.framingOffset = new Vector2(-2f, 1.5f);
        settings.autoZoom = false;
        settings.authoredZoom = 1f;
        cameras.AcquirePresentation(settings, new[] { focus }, this);

        ResolveFraming();
        SnapCamera();

        Assert.That(controller.RenderedPosition.x, Is.EqualTo(12f).Within(0.001f));
        Assert.That(controller.RenderedPosition.y, Is.EqualTo(4.5f).Within(0.001f));
        Assert.That(controller.PresentationFraming.ValidTargetCount, Is.EqualTo(1));
    }

    [Test]
    public void FocusWorldPointFramesAnAuthoredPositionWithNoTargets()
    {
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusWorldPoint);
        settings.worldPoint = new Vector2(-18f, 7f);
        settings.autoZoom = false;
        settings.authoredZoom = 1f;
        cameras.AcquirePresentation(settings, null, this);

        ResolveFraming();
        SnapCamera();

        Assert.That(controller.RenderedPosition.x, Is.EqualTo(-18f).Within(0.001f));
        Assert.That(controller.RenderedPosition.y, Is.EqualTo(7f).Within(0.001f));
    }

    [Test]
    public void FrameTargetsCentresOnTwoTargets()
    {
        Transform a = CreateTarget("A", new Vector3(-6f, 0f, 0f));
        Transform b = CreateTarget("B", new Vector3(10f, 4f, 0f));
        cameras.AcquirePresentation(BuildFrameSettings(0f, 0f), new[] { a, b }, this);

        ResolveFraming();
        SnapCamera();

        Assert.That(controller.PresentationFraming.FramedBounds.center.x, Is.EqualTo(2f).Within(0.001f));
        Assert.That(controller.PresentationFraming.FramedBounds.center.y, Is.EqualTo(2f).Within(0.001f));
        Assert.That(controller.RenderedPosition.x, Is.EqualTo(2f).Within(0.001f));
        Assert.That(controller.RenderedPosition.y, Is.EqualTo(2f).Within(0.001f));
    }

    [Test]
    public void FrameTargetsEncapsulatesEveryValidTarget()
    {
        Transform a = CreateTarget("A", new Vector3(-10f, -4f, 0f));
        Transform b = CreateTarget("B", new Vector3(0f, 8f, 0f));
        Transform c = CreateTarget("C", new Vector3(14f, 2f, 0f));
        cameras.AcquirePresentation(BuildFrameSettings(0f, 0f), new[] { a, b, c }, this);

        ResolveFraming();
        SnapCamera();

        Bounds framed = controller.PresentationFraming.FramedBounds;
        Assert.That(controller.PresentationFraming.ValidTargetCount, Is.EqualTo(3));
        Assert.That(framed.min.x, Is.EqualTo(-10f).Within(0.001f));
        Assert.That(framed.max.x, Is.EqualTo(14f).Within(0.001f));
        Assert.That(framed.min.y, Is.EqualTo(-4f).Within(0.001f));
        Assert.That(framed.max.y, Is.EqualTo(8f).Within(0.001f));
    }

    [Test]
    public void AutomaticZoomFitsTheRegionWidth()
    {
        // Half-width required = 20 + 4 padding = 24; expected zoom = 24 / baseHalfWidth.
        Transform a = CreateTarget("A", new Vector3(-20f, 0f, 0f));
        Transform b = CreateTarget("B", new Vector3(20f, 0f, 0f));
        cameras.AcquirePresentation(BuildFrameSettings(4f, 0f, minZoom: 0.1f, maxZoom: 10f), new[] { a, b }, this);

        ResolveFraming();

        float expected = 24f / BaseHalfWidth;
        Assert.That(controller.PresentationFraming.DesiredZoom, Is.EqualTo(expected).Within(0.001f));
        Assert.That(controller.PresentationFraming.ZoomClamped, Is.False);
    }

    [Test]
    public void PaddingIncreasesRequiredZoom()
    {
        Transform a = CreateTarget("A", new Vector3(-20f, 0f, 0f));
        Transform b = CreateTarget("B", new Vector3(20f, 0f, 0f));

        CameraPresentationHandle tight = cameras.AcquirePresentation(
            BuildFrameSettings(0f, 0f, minZoom: 0.1f, maxZoom: 10f), new[] { a, b }, this);
        ResolveFraming();
        float withoutPadding = controller.PresentationFraming.DesiredZoom;
        tight.Release();

        cameras.AcquirePresentation(
            BuildFrameSettings(6f, 0f, minZoom: 0.1f, maxZoom: 10f), new[] { a, b }, this);
        ResolveFraming();
        float withPadding = controller.PresentationFraming.DesiredZoom;

        Assert.That(withPadding, Is.GreaterThan(withoutPadding));
        Assert.That(withPadding - withoutPadding, Is.EqualTo(6f / BaseHalfWidth).Within(0.001f));
    }

    [Test]
    public void SharedPaddingAddsToRequestPadding()
    {
        SetField(controller, "presentationPaddingX", 3f);

        Transform a = CreateTarget("A", new Vector3(-20f, 0f, 0f));
        Transform b = CreateTarget("B", new Vector3(20f, 0f, 0f));
        cameras.AcquirePresentation(BuildFrameSettings(4f, 0f, minZoom: 0.1f, maxZoom: 10f), new[] { a, b }, this);

        ResolveFraming();

        // 20 half-width + 3 shared + 4 per-request.
        Assert.That(controller.PresentationFraming.Padding.x, Is.EqualTo(7f).Within(0.0001f));
        Assert.That(controller.PresentationFraming.DesiredZoom, Is.EqualTo(27f / BaseHalfWidth).Within(0.001f));
    }

    [Test]
    public void AutomaticZoomIsClampedByTheRequestedMaximum()
    {
        Transform a = CreateTarget("A", new Vector3(-200f, 0f, 0f));
        Transform b = CreateTarget("B", new Vector3(200f, 0f, 0f));
        cameras.AcquirePresentation(BuildFrameSettings(0f, 0f, minZoom: 0.5f, maxZoom: 1.25f), new[] { a, b }, this);

        ResolveFraming();

        Assert.That(controller.PresentationFraming.DesiredZoom, Is.GreaterThan(1.25f));
        Assert.That(controller.PresentationFraming.ClampedZoom, Is.EqualTo(1.25f).Within(0.0001f));
        Assert.That(controller.PresentationFraming.ZoomClamped, Is.True);
    }

    [Test]
    public void AutomaticZoomIsClampedByTheRequestedMinimum()
    {
        Transform a = CreateTarget("A", new Vector3(-0.2f, 0f, 0f));
        Transform b = CreateTarget("B", new Vector3(0.2f, 0f, 0f));
        cameras.AcquirePresentation(BuildFrameSettings(0f, 0f, minZoom: 0.9f, maxZoom: 1.4f), new[] { a, b }, this);

        ResolveFraming();

        Assert.That(controller.PresentationFraming.DesiredZoom, Is.LessThan(0.9f));
        Assert.That(controller.PresentationFraming.ClampedZoom, Is.EqualTo(0.9f).Within(0.0001f));
        Assert.That(controller.PresentationFraming.ZoomClamped, Is.True);
    }

    [Test]
    public void RoomBoundsCapZoomSoNothingOutsideTheRoomIsRevealed()
    {
        // A room only slightly taller than the authored viewport must cap zoom-out.
        float roomHalfHeight = BaseHalfHeight * 1.1f;
        CameraBoundsVolume volume = CreateBounds(
            "Room",
            new Vector2(BaseHalfWidth * 4f, roomHalfHeight * 2f),
            Vector2.zero);
        controller.SetBoundsVolume(volume);

        Transform a = CreateTarget("A", new Vector3(-200f, 0f, 0f));
        Transform b = CreateTarget("B", new Vector3(200f, 0f, 0f));
        cameras.AcquirePresentation(BuildFrameSettings(0f, 0f, minZoom: 0.5f, maxZoom: 4f), new[] { a, b }, this);

        ResolveFraming();

        Assert.That(controller.PresentationFraming.MaxZoom, Is.EqualTo(1.1f).Within(0.01f));
        Assert.That(controller.PresentationFraming.ClampedZoom, Is.LessThanOrEqualTo(1.11f));
    }

    [Test]
    public void RoomSmallerThanTheViewportNeverForcesAZoomIn()
    {
        CameraBoundsVolume volume = CreateBounds("Tiny Room", new Vector2(4f, 3f), Vector2.zero);
        controller.SetBoundsVolume(volume);

        Transform a = CreateTarget("A", new Vector3(-30f, 0f, 0f));
        Transform b = CreateTarget("B", new Vector3(30f, 0f, 0f));
        cameras.AcquirePresentation(BuildFrameSettings(0f, 0f, minZoom: 0.5f, maxZoom: 3f), new[] { a, b }, this);

        ResolveFraming();

        Assert.That(controller.PresentationFraming.MaxZoom, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(controller.PresentationFraming.ClampedZoom, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void PresentationCentreIsClampedByTheLegalRoomRegion()
    {
        CameraBoundsVolume volume = CreateBounds("Room", new Vector2(60f, 40f), Vector2.zero);
        controller.SetBoundsVolume(volume);

        // Far outside the room: the legal-centre region must win over the requested focus.
        Transform focus = CreateTarget("Focus", new Vector3(500f, 500f, 0f));
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
        settings.autoZoom = false;
        settings.authoredZoom = 1f;
        cameras.AcquirePresentation(settings, new[] { focus }, this);

        ResolveFraming();
        SnapCamera();

        float maxX = 30f - BaseHalfWidth;
        float maxY = 20f - BaseHalfHeight;
        Assert.That(controller.RenderedPosition.x, Is.EqualTo(maxX).Within(0.01f));
        Assert.That(controller.RenderedPosition.y, Is.EqualTo(maxY).Within(0.01f));
        Assert.That(controller.PresentationFraming.CentreClamped, Is.True);
    }

    [Test]
    public void PresentationCentreCollapsesDeterministicallyInsideALockSmallerThanTheViewport()
    {
        CameraLockArea area = CreateLock("Tiny", 1, size: new Vector2(2f, 2f), center: new Vector2(5f, 5f));
        controller.EnterLockArea(area);

        Transform focus = CreateTarget("Focus", new Vector3(400f, 400f, 0f));
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
        settings.autoZoom = false;
        settings.authoredZoom = 1f;
        cameras.AcquirePresentation(settings, new[] { focus }, this);

        ResolveFraming();
        SnapCamera();

        Assert.That(controller.RenderedPosition.x, Is.EqualTo(5f).Within(0.001f));
        Assert.That(controller.RenderedPosition.y, Is.EqualTo(5f).Within(0.001f));
    }

    [Test]
    public void TinyTargetMovementDoesNotChangeTheZoomTarget()
    {
        Transform a = CreateTarget("A", new Vector3(-20f, 0f, 0f));
        Transform b = CreateTarget("B", new Vector3(20f, 0f, 0f));
        cameras.AcquirePresentation(BuildFrameSettings(0f, 0f, minZoom: 0.1f, maxZoom: 5f), new[] { a, b }, this);

        ResolveFraming();
        Invoke(controller, "UpdateZoom");
        float settled = controller.TargetZoom;

        // A jitter far smaller than the hysteresis band must not re-commit the zoom target.
        b.position = new Vector3(20.02f, 0f, 0f);
        ResolveFraming();
        Invoke(controller, "UpdateZoom");

        Assert.That(controller.TargetZoom, Is.EqualTo(settled).Within(1e-6f));
    }

    [Test]
    public void LargeTargetMovementDoesChangeTheZoomTarget()
    {
        Transform a = CreateTarget("A", new Vector3(-20f, 0f, 0f));
        Transform b = CreateTarget("B", new Vector3(20f, 0f, 0f));
        cameras.AcquirePresentation(BuildFrameSettings(0f, 0f, minZoom: 0.1f, maxZoom: 5f), new[] { a, b }, this);

        ResolveFraming();
        Invoke(controller, "UpdateZoom");
        float settled = controller.TargetZoom;

        b.position = new Vector3(60f, 0f, 0f);
        ResolveFraming();
        Invoke(controller, "UpdateZoom");

        Assert.That(controller.TargetZoom, Is.GreaterThan(settled));
    }

    [Test]
    public void ZoomIsAppliedAsFieldOfViewAndNeverMovesTheCameraAlongZ()
    {
        float zBefore = controller.transform.position.z;
        Transform focus = CreateTarget("Focus", Vector3.zero);
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
        settings.autoZoom = false;
        settings.authoredZoom = 1.5f;
        settings.overrideZoomLimits = true;
        settings.minZoom = 0.5f;
        settings.maxZoom = 2f;
        cameras.AcquirePresentation(settings, new[] { focus }, this);

        ResolveFraming();
        SnapCamera();

        float expectedFov = 2f * Mathf.Atan(Mathf.Tan(24f * 0.5f * Mathf.Deg2Rad) * 1.5f) * Mathf.Rad2Deg;
        Assert.That(controller.CurrentZoom, Is.EqualTo(1.5f).Within(0.0001f));
        Assert.That(cam.fieldOfView, Is.EqualTo(expectedFov).Within(0.001f));
        Assert.That(controller.transform.position.z, Is.EqualTo(zBefore).Within(0.0001f));
    }

    [Test]
    public void ZoomReturnsToBaseAfterTheRequestIsReleased()
    {
        Transform focus = CreateTarget("Focus", Vector3.zero);
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
        settings.autoZoom = false;
        settings.authoredZoom = 1.4f;
        settings.overrideZoomLimits = true;
        settings.minZoom = 0.5f;
        settings.maxZoom = 2f;
        CameraPresentationHandle handle = cameras.AcquirePresentation(settings, new[] { focus }, this);

        ResolveFraming();
        SnapCamera();
        Assert.That(controller.CurrentZoom, Is.EqualTo(1.4f).Within(0.0001f));

        handle.Release();
        ResolveFraming();
        SnapCamera();

        Assert.That(controller.CurrentZoom, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(cam.fieldOfView, Is.EqualTo(24f).Within(0.001f));
    }

    [Test]
    public void ClipWeightBlendsBetweenUnderlyingAndPresentationFraming()
    {
        hero.transform.position = Vector3.zero;
        target.SceneInit();
        SetField(target, "currentVerticalOffset", 0f);

        Transform focus = CreateTarget("Focus", new Vector3(20f, 0f, 0f));
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
        settings.autoZoom = false;
        settings.authoredZoom = 1f;
        settings.weight = 0.5f;
        cameras.AcquirePresentation(settings, new[] { focus }, this);

        ResolveFraming();
        SnapCamera();

        Assert.That(controller.RenderedPosition.x, Is.EqualTo(10f).Within(0.001f));
    }

    [Test]
    public void UnderlyingDestinationIsReportedWhileThePresentationIsActive()
    {
        hero.transform.position = new Vector3(3f, 1f, 0f);
        target.SceneInit();
        SetField(target, "currentVerticalOffset", 0f);

        AcquireFocusTarget(CreateTarget("Focus", new Vector3(40f, 20f, 0f)), 0);
        ResolveFraming();
        SnapCamera();

        Assert.That(controller.UnderlyingDestination.x, Is.EqualTo(3f).Within(0.001f));
        Assert.That(controller.UnderlyingDestination.y, Is.EqualTo(1f).Within(0.001f));
        Assert.That(controller.RenderedPosition.x, Is.EqualTo(40f).Within(0.001f));
    }

    [Test]
    public void SnapshotsExposeReadOnlyRequestDiagnostics()
    {
        Transform focus = CreateTarget("Boss", new Vector3(4f, 0f, 0f));
        cameras.AcquirePresentation(
            CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget),
            new[] { focus },
            this,
            CameraRequestLifetime.Scene,
            7);

        IReadOnlyList<CameraPresentationSnapshot> snapshots = cameras.GetPresentationSnapshots();

        Assert.That(snapshots.Count, Is.EqualTo(1));
        Assert.That(snapshots[0].Priority, Is.EqualTo(7));
        Assert.That(snapshots[0].Mode, Is.EqualTo(CameraPresentationMode.FocusTarget));
        Assert.That(snapshots[0].ValidTargetCount, Is.EqualTo(1));
        Assert.That(snapshots[0].TargetLabel, Does.Contain("Boss"));
        Assert.That(snapshots[0].IsSelected, Is.True);
        Assert.That(snapshots[0].RemainingSeconds, Is.LessThan(0f));
        Assert.That(snapshots[0].Lifetime, Is.EqualTo(CameraRequestLifetime.Scene));
    }

    [Test]
    public void HandleUpdateReAuthorsInPlaceWithoutChangingSelectionOrder()
    {
        CameraPresentationHandle older = AcquireFocusTarget(CreateTarget("Older", Vector3.zero), 5);
        CameraPresentationHandle newer = AcquireFocusTarget(CreateTarget("Newer", Vector3.one), 5);
        Assert.That(newer.IsSelected, Is.True);

        CameraPresentationSettings updated = CameraPresentationSettings.Default(CameraPresentationMode.FocusWorldPoint);
        updated.worldPoint = new Vector2(11f, 0f);
        Assert.That(older.Update(updated), Is.True);

        Assert.That(newer.IsSelected, Is.True, "An in-place update must not steal selection.");
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(2));
    }

    [TestCase(float.NaN, 1f, 1f, false)]
    [TestCase(1f, float.PositiveInfinity, 1f, false)]
    [TestCase(1f, 1f, 1f, true)]
    public void NonFiniteSettingsAreDetected(float padding, float authoredZoom, float weight, bool expectedFinite)
    {
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
        settings.paddingX = padding;
        settings.authoredZoom = authoredZoom;
        settings.weight = weight;

        Assert.That(settings.IsFinite(), Is.EqualTo(expectedFinite));
    }

    [Test]
    public void NegativePaddingAndInvertedZoomLimitsAreDetected()
    {
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FrameTargets);
        settings.paddingY = -1f;
        Assert.That(settings.IsNonNegative(), Is.False);

        settings = CameraPresentationSettings.Default(CameraPresentationMode.FrameTargets);
        settings.overrideZoomLimits = true;
        settings.minZoom = 2f;
        settings.maxZoom = 1f;
        Assert.That(settings.HasValidZoomLimitOrder(), Is.False);
    }

    // --------------------------------------------------------------------------------- helpers

    // Unity recomputes Camera.aspect from the Game view between editor frames, and the test runner
    // spans frames, so the aspect is re-forced immediately before anything that reads it.
    private void ResolveFraming()
    {
        cam.aspect = Aspect;
        Invoke(controller, "ResolvePresentationFraming");
    }

    private void SnapCamera()
    {
        cam.aspect = Aspect;
        controller.SnapToTarget();
    }

    private CameraPresentationHandle AcquireFocusTarget(Transform focus, int priority)
    {
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
        settings.autoZoom = false;
        settings.authoredZoom = 1f;
        return cameras.AcquirePresentation(settings, new[] { focus }, this, CameraRequestLifetime.Scene, priority);
    }

    private static CameraPresentationSettings BuildFrameSettings(
        float paddingX,
        float paddingY,
        float minZoom = 0.85f,
        float maxZoom = 1.6f)
    {
        CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FrameTargets);
        settings.paddingX = paddingX;
        settings.paddingY = paddingY;
        settings.autoZoom = true;
        settings.overrideZoomLimits = true;
        settings.minZoom = minZoom;
        settings.maxZoom = maxZoom;
        return settings;
    }

    private Transform CreateTarget(string name, Vector3 position)
    {
        GameObject go = new GameObject(name);
        go.transform.position = position;
        temporaries.Add(go);
        return go.transform;
    }

    private CameraLockArea CreateLock(
        string name,
        int priority,
        Vector2? size = null,
        Vector2? center = null)
    {
        GameObject go = new GameObject(name);
        go.transform.position = center ?? Vector2.zero;
        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = size ?? new Vector2(200f, 150f);
        CameraLockArea area = go.AddComponent<CameraLockArea>();
        SetField(area, "priority", priority);
        SetField(area, "lockX", true);
        SetField(area, "lockY", true);
        temporaries.Add(go);
        return area;
    }

    private CameraBoundsVolume CreateBounds(string name, Vector2 size, Vector2 center)
    {
        GameObject go = new GameObject(name);
        go.transform.position = center;
        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = size;
        CameraBoundsVolume volume = go.AddComponent<CameraBoundsVolume>();
        temporaries.Add(go);
        return volume;
    }

    // GameCameras.Awake normally installs the singleton. Adding the component in EditMode does not
    // reliably run it, and CameraEventService-based sources need Instance, so it is installed here
    // without ever creating a second one.
    private void EnsureCamerasInstance()
    {
        if (GameCameras.Instance == cameras)
        {
            return;
        }

        if (GameCameras.Instance != null && GameCameras.Instance != cameras)
        {
            Object.DestroyImmediate(GameCameras.Instance.gameObject);
        }

        typeof(GameCameras)
            .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, cameras);
    }

    private static object Invoke(object targetObject, string method, params object[] arguments)
    {
        foreach (MethodInfo candidate in targetObject.GetType().GetMethods(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (candidate.Name == method && candidate.GetParameters().Length == arguments.Length)
            {
                return candidate.Invoke(targetObject, arguments);
            }
        }

        Assert.Fail($"Method '{method}' was not found on {targetObject.GetType().Name}.");
        return null;
    }

    private static void SetField(object targetObject, string field, object value)
    {
        targetObject.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SetValue(targetObject, value);
    }
}
