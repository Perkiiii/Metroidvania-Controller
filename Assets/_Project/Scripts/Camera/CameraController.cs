using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
public sealed class CameraController : MonoBehaviour
{
    private sealed class LockRegistration
    {
        public CameraLockArea Area;
        public int Priority;
        public long EntrySequence;
        public int SceneHandle;
    }

    private readonly struct AxisInterval
    {
        public AxisInterval(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Min { get; }
        public float Max { get; }
    }

    [Header("Config")]
    [SerializeField] private CameraConfig config;

    [Header("References")]
    [SerializeField] private CameraTarget cameraTarget;
    [SerializeField] private Transform cameraParent;

    [Header("Smoothing")]
    [SerializeField] private float dampTimeNormal = 0.25f;
    [SerializeField] private float dampTimeSlow = 0.15f;

    [Header("Motion")]
    [SerializeField] private float maxVelocity = 65f;
    [SerializeField] private float lookOffset;

    [Header("Projection")]
    [SerializeField] private float fieldOfView = 24f;
    [SerializeField] private float cameraZ = -38.1f;

    [Header("Scene Start")]
    [SerializeField] private float startLockedTimer = 0.65f;

    [Header("Lock Transition Fallbacks")]
    [SerializeField] private CameraTransitionSettings sceneStartTransition = CameraTransitionSettings.Immediate();
    [SerializeField] private CameraTransitionSettings followToLockTransition = CameraTransitionSettings.Live(0.15f, 0.35f, false);
    [SerializeField] private CameraTransitionSettings lockToLockTransition = CameraTransitionSettings.Live(0.15f, 0.35f, false);
    [SerializeField] private CameraTransitionSettings lockToFollowTransition = CameraTransitionSettings.Live(0.15f, 0.35f, false);
    [SerializeField] private CameraTransitionSettings overrideReleasedTransition = CameraTransitionSettings.Live(0.15f, 0.35f, true);

    [Header("Presentation Fallbacks (Camera Phase 3)")]
    [SerializeField] private CameraTransitionSettings presentationEnterTransition = CameraTransitionSettings.Live(0.22f, 0.45f, false);
    [SerializeField] private CameraTransitionSettings presentationChangeTransition = CameraTransitionSettings.Live(0.22f, 0.45f, false);
    [SerializeField] private CameraTransitionSettings presentationReleaseTransition = CameraTransitionSettings.Live(0.2f, 0.45f, true);
    [SerializeField] private float presentationPaddingX = 3f;
    [SerializeField] private float presentationPaddingY = 2f;
    [SerializeField] private float minZoom = 0.85f;
    [SerializeField] private float maxZoom = 1.6f;
    [SerializeField] private float zoomOutDampTime = 0.25f;
    [SerializeField] private float zoomInDampTime = 0.55f;
    [SerializeField] private float maxZoomSpeed = 1.2f;
    [SerializeField] private float zoomHysteresis = 0.02f;
    [SerializeField] private float zoomContractHysteresis = 0.06f;
    [SerializeField] private float presentationCentreDampTime = 0.25f;

    [Header("Presentation Gizmos")]
    [Tooltip("Draw the resolved presentation framing region, padding, desired centre, and viewport while selected in Play Mode.")]
    [SerializeField] private bool drawPresentationGizmos = true;

    public static bool IsPositioningCamera { get; private set; }

    public event Action PositionedAtHero;

    public CameraMode Mode { get; private set; }
    public float LookOffset
    {
        get => lookOffset;
        set
        {
            lookOffset = value;
            lookOffsetTarget = value;
        }
    }
    public CameraLockArea CurrentLockArea => GetActiveLockRegistration()?.Area;
    public CameraBoundsVolume CurrentBoundsVolume => GetActiveBoundsVolume();
    public int ActiveBoundsCount
    {
        get
        {
            GetActiveBoundsVolume();
            return boundsStack.Count;
        }
    }
    public int ActiveLockCount
    {
        get
        {
            RemoveInvalidLockRegistrations();
            return lockRegistrations.Count;
        }
    }
    public long CurrentLockEntrySequence => GetActiveLockRegistration()?.EntrySequence ?? 0L;
    public float LookInputThreshold => config != null ? config.lookInputThreshold : 0.5f;
    public float ManualLookHoldDelay => config != null ? config.manualLookHoldDelay : 2f;

    public CameraTransitionCause CurrentTransitionCause => transitionCause;
    public bool IsTransitioning => transitionActive;
    public CameraLockArea TransitionSourceLockArea => transitionSourceLock;
    public CameraLockArea TransitionDestinationLockArea => transitionDestinationLock;
    public float TransitionElapsed => transitionElapsed;
    public float TransitionDuration => transitionBlendDuration;
    public float TransitionProgress => transitionBlendDuration > 0f ? Mathf.Clamp01(transitionElapsed / transitionBlendDuration) : 1f;
    public bool LastApplicationWasImmediate => lastApplicationWasImmediate;
    public Vector3 CurrentDestination => lastComputedDestination;
    public Vector3 RenderedPosition => transform.position;
    public float CurrentDampTimeX => currentDampX;
    public float CurrentDampTimeY => currentDampY;

    // --- Camera Phase 3 presentation diagnostics (read-only) ---

    // True while a presentation request is registered as selected, even if the scene-start timer
    // is still suppressing its influence.
    public bool HasPresentationRequest => presentationActive;
    public long PresentationRequestId => presentationActive ? presentationId : 0L;
    public int PresentationPriority => presentationPriority;
    public string PresentationSourceLabel => presentationSourceLabel;
    public CameraPresentationSettings PresentationSettings => presentationSettings;
    public CameraPresentationFraming PresentationFraming => presentationFraming;

    // The base hero/room/lock destination the camera resumes when the request is released.
    public Vector3 UnderlyingDestination => lastUnderlyingDestination;

    // Rendered zoom multiplier of the authored base viewport (1 = CameraConfig framing).
    public float CurrentZoom => currentZoom;
    public float TargetZoom => targetZoom;
    public float BaseViewportHalfHeight => baseHalfHeight;

    private readonly List<CameraBoundsVolume> boundsStack = new List<CameraBoundsVolume>();
    private readonly List<LockRegistration> lockRegistrations = new List<LockRegistration>();

    private Camera cam;
    private Coroutine positioningRoutine;
    private Vector3 velocityX;
    private Vector3 velocityY;
    private float currentDampX;
    private float currentDampY;
    private float lookSlowTimer;
    private float startTimer;
    private float lookOffsetTarget;
    private long nextLockEntrySequence;
    private bool freezeOverride;
    private bool freezeTargetOverride;
    private bool freeOverride;
    private bool positioningOverride;

    private CameraTransitionCause transitionCause = CameraTransitionCause.None;
    private CameraLockArea transitionSourceLock;
    private CameraLockArea transitionDestinationLock;
    private bool transitionActive;
    private bool lastApplicationWasImmediate = true;
    private float transitionElapsed;
    private float transitionBlendDuration;
    private float transitionStartDampX;
    private float transitionStartDampY;
    private Vector3 lastComputedDestination;
    private Vector3 lastUnderlyingDestination;

    private readonly List<Transform> presentationTargets = new List<Transform>();
    private bool presentationActive;
    private long presentationId;
    private int presentationPriority;
    private string presentationSourceLabel = "";
    private CameraPresentationSettings presentationSettings;
    private CameraPresentationFraming presentationFraming;
    private bool presentationFramingResolved;
    private Vector3 presentationCentre;
    private float presentationDesiredZoom = 1f;

    private float baseHalfHeight;
    private float currentZoom = 1f;
    private float targetZoom = 1f;
    private float zoomVelocity;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        ApplyConfig();
        EnforceProjection();
        CameraInfoCache.UpdateCache(cam, true);
    }

    public void SetConfig(CameraConfig cameraConfig)
    {
        config = cameraConfig;
        ApplyConfig();
        EnforceProjection();
        CameraInfoCache.UpdateCache(cam, true);
    }

    public void SceneInit()
    {
        if (positioningRoutine != null)
        {
            StopCoroutine(positioningRoutine);
            positioningRoutine = null;
        }

        positioningOverride = false;
        IsPositioningCamera = false;
        freeOverride = false;

        // Presentation payload is scene-scoped state on the controller; GameCameras owns the
        // registrations and re-pushes any deliberately persistent request after entry.
        presentationActive = false;
        presentationId = 0L;
        presentationPriority = 0;
        presentationSourceLabel = "";
        presentationTargets.Clear();
        presentationFramingResolved = false;
        presentationFraming = default;
        presentationDesiredZoom = 1f;

        ApplyConfig();
        EnforceProjection();
        boundsStack.Clear();
        lockRegistrations.Clear();
        nextLockEntrySequence = 0L;
        RefreshResolvedMode();
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;
        currentDampX = dampTimeNormal;
        currentDampY = GetGroundedDampTimeY();
        lookOffset = 0f;
        lookOffsetTarget = 0f;
        lookSlowTimer = 0f;
        ResetStartTimer();
        EndTransition();
        lastApplicationWasImmediate = true;

        GameObject hero = GameObject.FindWithTag("Player");
        if (hero == null)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapPointAll(hero.transform.position);
        foreach (Collider2D hit in hits)
        {
            CameraBoundsVolume boundsVolume = hit.GetComponent<CameraBoundsVolume>();
            if (boundsVolume != null && !boundsStack.Contains(boundsVolume))
            {
                boundsStack.Add(boundsVolume);
            }
        }

        transform.position = new Vector3(hero.transform.position.x, hero.transform.position.y, transform.position.z);
        CameraInfoCache.UpdateCache(cam, true);
    }

    public void SetBoundsVolume(CameraBoundsVolume volume)
    {
        if (volume != null && !boundsStack.Contains(volume))
        {
            boundsStack.Add(volume);
        }
    }

    public void ClearBoundsVolume(CameraBoundsVolume volume)
    {
        boundsStack.Remove(volume);
    }

    public void EnterLockArea(CameraLockArea area)
    {
        if (area == null || FindRegistration(area) != null)
        {
            return;
        }

        CameraLockArea previous = CurrentLockArea;
        EnsureSequenceCapacity();
        lockRegistrations.Add(new LockRegistration
        {
            Area = area,
            Priority = area.Priority,
            EntrySequence = ++nextLockEntrySequence,
            SceneHandle = area.gameObject.scene.handle
        });
        RefreshActiveLock(previous);
    }

    public void ExitLockArea(CameraLockArea area)
    {
        LockRegistration registration = FindRegistration(area);
        if (registration == null)
        {
            return;
        }

        CameraLockArea previous = CurrentLockArea;
        lockRegistrations.Remove(registration);
        if (previous == area)
        {
            RefreshActiveLock(previous);
        }
    }

    public void ClearSceneRegistrations(Scene scene)
    {
        CameraLockArea previous = CurrentLockArea;
        lockRegistrations.RemoveAll(registration =>
            registration == null
            || registration.Area == null
            || registration.SceneHandle == scene.handle);
        boundsStack.RemoveAll(volume =>
            volume == null || volume.gameObject.scene.handle == scene.handle);

        if (previous != CurrentLockArea)
        {
            RefreshActiveLock(previous);
        }
    }

    // Called by GameCameras with the request it selected. The controller never chooses between
    // requests; it only resolves the selected one into framing, zoom, and a legal destination.
    public void ApplyPresentation(
        long requestId,
        int priority,
        in CameraPresentationSettings settings,
        Transform[] targets,
        string sourceLabel,
        CameraTransitionCause cause)
    {
        bool wasActive = presentationActive;
        presentationActive = true;
        presentationId = requestId;
        presentationPriority = priority;
        presentationSettings = settings;
        presentationSourceLabel = string.IsNullOrEmpty(sourceLabel) ? "(none)" : sourceLabel;

        presentationTargets.Clear();
        for (int i = 0; targets != null && i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                presentationTargets.Add(targets[i]);
            }
        }

        if (cause == CameraTransitionCause.None)
        {
            return;
        }

        // A newly selected request behaves exactly like a lock change: registration always
        // applies, but the live blend is suppressed while an override or the scene-start snap owns
        // the rendered camera.
        if (!AreLiveTransitionsAllowed())
        {
            return;
        }

        CameraTransitionCause resolved = wasActive
            ? CameraTransitionCause.PresentationChanged
            : cause;
        BeginTransition(
            resolved,
            ResolvePresentationTransitionSettings(resolved, settings),
            CurrentLockArea,
            CurrentLockArea);
    }

    public void ClearPresentation()
    {
        if (!presentationActive)
        {
            return;
        }

        CameraPresentationSettings released = presentationSettings;
        presentationActive = false;
        presentationId = 0L;
        presentationPriority = 0;
        presentationSourceLabel = "";
        presentationTargets.Clear();
        presentationFramingResolved = false;
        presentationFraming = default;
        presentationDesiredZoom = 1f;

        if (!AreLiveTransitionsAllowed())
        {
            return;
        }

        // Resolve toward whatever the underlying framing is *now* -- never a snapshot captured
        // when the request began.
        BeginTransition(
            CameraTransitionCause.PresentationReleased,
            ResolvePresentationTransitionSettings(CameraTransitionCause.PresentationReleased, released),
            CurrentLockArea,
            CurrentLockArea);
    }

    public void SetLookInput(float verticalInput)
    {
        if (Mathf.Approximately(verticalInput, 0f))
        {
            lookOffsetTarget = 0f;
            return;
        }

        float amount = config != null ? config.lookInputOffset : 4.5f;
        lookOffsetTarget = Mathf.Sign(verticalInput) * amount;
        lookSlowTimer = config != null ? config.lookInputSlowTime : 0.35f;
    }

    public void ResetStartTimer()
    {
        startTimer = startLockedTimer;
    }

    public void SetFrozen(bool frozen)
    {
        if (frozen)
        {
            FreezeInPlace();
        }
        else
        {
            StopFreeze();
        }
    }

    public void FreezeInPlace(bool freezeTarget = false)
    {
        ApplyFreezeState(true, freezeTarget);
    }

    public void ApplyFreezeState(bool frozen, bool freezeTarget)
    {
        ApplyFreezeState(frozen, freezeTarget, false);
    }

    public void ApplyFreezeState(bool frozen, bool freezeTarget, bool suppressReleaseTransition)
    {
        bool wasFrozen = freezeOverride;
        freezeOverride = frozen;
        freezeTargetOverride = frozen && freezeTarget;
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;
        RefreshResolvedMode();
        RefreshTargetMode();

        if (wasFrozen && !frozen && !freeOverride)
        {
            if (suppressReleaseTransition)
            {
                EndTransition();
                lastApplicationWasImmediate = true;
            }
            else
            {
                BeginOverrideReleaseTransition();
            }
        }
    }

    public void StopFreeze(bool stopFreezeTarget = false)
    {
        ApplyFreezeState(false, false);
    }

    public void StartFreeMode(bool keepTargetHorizontalOffset = true)
    {
        freeOverride = true;
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;
        cameraTarget?.StartFreeMode(keepTargetHorizontalOffset);
        RefreshResolvedMode();
    }

    public void EndFreeMode()
    {
        freeOverride = false;
        RefreshResolvedMode();
        RefreshTargetMode();

        if (!freezeOverride)
        {
            BeginOverrideReleaseTransition();
        }
    }

    public Coroutine PositionToHero(bool freezeTarget = true)
    {
        if (positioningRoutine != null)
        {
            StopCoroutine(positioningRoutine);
        }

        positioningRoutine = StartCoroutine(PositionToHeroRoutine(freezeTarget));
        return positioningRoutine;
    }

    public void SnapToTarget()
    {
        if (cameraTarget == null)
        {
            return;
        }

        SnapZoom();
        transform.position = ComputeDestination();
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;
        CameraInfoCache.UpdateCache(cam, true);
        EndTransition();
        lastApplicationWasImmediate = true;
    }

    // Immediate applications must land on the final projection too, otherwise the rendered frame
    // would still be blending zoom after a "snap".
    private void SnapZoom()
    {
        targetZoom = PresentationInfluencesFraming
            ? Mathf.LerpUnclamped(1f, presentationDesiredZoom, presentationSettings.ResolvedWeight)
            : 1f;
        currentZoom = targetZoom;
        zoomVelocity = 0f;
        ApplyZoomToProjection();
    }

    public bool ApplySceneEntryImmediate(out string failureDetail)
    {
        failureDetail = "";
        if (cameraTarget == null)
        {
            failureDetail = "CameraController has no CameraTarget reference.";
            return false;
        }

        if (!cameraTarget.HasHeroBinding)
        {
            failureDetail = "CameraTarget is not bound to the positioned hero.";
            return false;
        }

        SnapToTarget();

        // The ordinary scene-start timer snaps while it counts down. A transition hard-freeze
        // pauses LateUpdate, so leaving the timer armed would defer those snaps until reveal
        // cleanup and visibly correct the camera after the hero's entry motion.
        startTimer = 0f;
        currentDampX = dampTimeNormal;
        currentDampY = GetGroundedDampTimeY();

        Vector3 expected = ComputeDestination();
        if (!IsFinite(expected) || !IsFinite(transform.position))
        {
            failureDetail = "Camera scene-entry destination was not finite.";
            return false;
        }

        if ((transform.position - expected).sqrMagnitude > 0.0001f)
        {
            failureDetail = $"Rendered camera did not reach its scene-entry destination. Expected {expected}, got {transform.position}.";
            return false;
        }

        return true;
    }

    public bool ApplySceneEntryFallback(Vector3 heroPosition, out string failureDetail)
    {
        failureDetail = "";
        Vector3 fallback = new Vector3(heroPosition.x, heroPosition.y, transform.position.z);
        if (!IsFinite(fallback))
        {
            failureDetail = $"Fallback hero position {heroPosition} was not finite.";
            return false;
        }

        transform.position = fallback;
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;
        currentDampX = dampTimeNormal;
        currentDampY = GetGroundedDampTimeY();
        startTimer = 0f;
        ResetZoom();
        EndTransition();
        lastApplicationWasImmediate = true;
        lastComputedDestination = fallback;
        lastUnderlyingDestination = fallback;
        CameraInfoCache.UpdateCache(cam, true);
        return true;
    }

    public void SnapToY(float worldY)
    {
        Vector3 p = transform.position;
        p.y = worldY;
        transform.position = p;
        velocityY = Vector3.zero;
        CameraInfoCache.UpdateCache(cam, true);
    }

    private void LateUpdate()
    {
        if (cameraTarget == null)
        {
            return;
        }

        if (RemoveInvalidLockRegistrations())
        {
            RefreshResolvedMode();
            RefreshTargetMode();
        }

        cameraTarget.Tick();

        // Resolved every frame so read-only diagnostics stay live even while an override owns the
        // rendered camera. Nothing here writes the transform or projection.
        ResolvePresentationFraming();

        if (Mode == CameraMode.Frozen || Mode == CameraMode.Free || Time.timeScale <= Mathf.Epsilon)
        {
            // A hard freeze must also freeze zoom: the rendered frame stops changing entirely.
            CameraInfoCache.UpdateCache(cam, true);
            return;
        }

        if (startTimer > 0f)
        {
            startTimer -= Time.deltaTime;
            SnapToTarget();
            lookOffset = 0f;
            lookOffsetTarget = 0f;
            CameraInfoCache.UpdateCache(cam, true);
            return;
        }

        UpdateDampTimes();
        UpdateLookOffset();

        // Zoom is applied before the destination is clamped so the legal-region inset always uses
        // the half-extents the camera actually renders with this frame. Clamping against a
        // not-yet-reached target zoom could reveal space outside the room mid-blend.
        UpdateZoom();

        Vector3 destination = ComputeDestination();
        float z = transform.position.z;

        Vector3 nextX = Vector3.SmoothDamp(
            transform.position,
            new Vector3(destination.x, transform.position.y, z),
            ref velocityX,
            currentDampX,
            maxVelocity);

        Vector3 nextY = Vector3.SmoothDamp(
            transform.position,
            new Vector3(transform.position.x, destination.y, z),
            ref velocityY,
            currentDampY,
            maxVelocity);

        transform.position = new Vector3(nextX.x, nextY.y, z);
        CameraInfoCache.UpdateCache(cam, true);
    }

    private void UpdateLookOffset()
    {
        float speed = config != null ? config.lookInputMoveSpeed : 7f;
        lookOffset = Mathf.MoveTowards(lookOffset, lookOffsetTarget, speed * Time.deltaTime);
    }

    private IEnumerator PositionToHeroRoutine(bool freezeTarget)
    {
        IsPositioningCamera = true;

        if (freezeTarget)
        {
            cameraTarget?.FreezeInPlace();
        }

        yield return new WaitForFixedUpdate();

        cameraTarget?.SnapToHero();
        SnapToTarget();
        positioningOverride = true;
        RefreshResolvedMode();

        float settleTime = config != null ? config.positioningSettleTime : 0.1f;
        if (settleTime > 0f)
        {
            yield return new WaitForSeconds(settleTime);
        }

        positioningOverride = false;
        RefreshResolvedMode();
        CameraInfoCache.UpdateCache(cam, true);

        if (freezeTarget)
        {
            RefreshTargetMode();
        }

        IsPositioningCamera = false;
        positioningRoutine = null;
        PositionedAtHero?.Invoke();
    }

    private void UpdateDampTimes()
    {
        float minDamp = config != null ? config.cameraDampTimeYMin : 0.03f;

        // While a presentation owns framing, hero-motion-derived Y damping is meaningless: the
        // destination is a focus point or multi-target region, not the hero.
        bool presentationOwnsMotion = PresentationInfluencesFraming;
        float targetDampX = presentationOwnsMotion ? Mathf.Max(0f, presentationCentreDampTime) : dampTimeNormal;
        float targetDampY = presentationOwnsMotion
            ? Mathf.Max(minDamp, presentationCentreDampTime)
            : Mathf.Max(minDamp, ResolveStateDampY());

        if (transitionActive)
        {
            transitionElapsed += Time.deltaTime;
            float t = transitionBlendDuration > 0f ? Mathf.Clamp01(transitionElapsed / transitionBlendDuration) : 1f;
            currentDampX = Mathf.Lerp(transitionStartDampX, targetDampX, t);
            currentDampY = Mathf.Lerp(transitionStartDampY, targetDampY, t);

            if (t >= 1f)
            {
                EndTransition();
            }

            return;
        }

        currentDampX = Mathf.MoveTowards(currentDampX, targetDampX, 0.007f);
        currentDampY = Mathf.MoveTowards(currentDampY, targetDampY, Time.deltaTime);
    }

    private float ResolveStateDampY()
    {
        if (lookSlowTimer > 0f)
        {
            lookSlowTimer -= Time.deltaTime;
            return config != null ? config.lookInputDampTimeY : 0.35f;
        }

        if (cameraTarget.IsFastFalling)
        {
            return config != null ? config.cameraDampTimeYFastFalling : 0.12f;
        }

        if (cameraTarget.IsFalling)
        {
            return config != null ? config.cameraDampTimeYFalling : 0.18f;
        }

        if (cameraTarget.IsRising)
        {
            return config != null ? config.cameraDampTimeYRising : 0.28f;
        }

        return GetGroundedDampTimeY();
    }

    private Vector3 ComputeDestination()
    {
        float z = transform.position.z;
        Vector3 underlying = new Vector3(
            cameraTarget.transform.position.x,
            cameraTarget.transform.position.y + cameraTarget.CurrentVerticalOffset + lookOffset,
            z);

        Vector3 clampedUnderlying = underlying;
        ClampToLegalRegion(ref clampedUnderlying);
        lastUnderlyingDestination = clampedUnderlying;

        Vector3 dest = underlying;
        if (PresentationInfluencesFraming)
        {
            // Timeline clip weight (and any authored partial weight) blends between the underlying
            // framing and the presentation framing rather than swapping handles.
            float weight = presentationSettings.ResolvedWeight;
            dest = Vector3.Lerp(underlying, new Vector3(presentationCentre.x, presentationCentre.y, z), weight);
        }

        Vector3 beforeClamp = dest;
        ClampToLegalRegion(ref dest);

        if (PresentationInfluencesFraming)
        {
            presentationFraming = new CameraPresentationFraming(
                true,
                presentationId,
                presentationSettings.mode,
                presentationFraming.ValidTargetCount,
                presentationFraming.FramedBounds,
                presentationFraming.Padding,
                new Vector3(beforeClamp.x, beforeClamp.y, z),
                presentationFraming.DesiredZoom,
                presentationFraming.ClampedZoom,
                presentationFraming.MinZoom,
                presentationFraming.MaxZoom,
                presentationFraming.ZoomClamped,
                (beforeClamp - dest).sqrMagnitude > 0.000001f,
                presentationSettings.ResolvedWeight);
        }

        lastComputedDestination = dest;
        return dest;
    }

    // A selected request only influences framing once the hidden scene-start snap window is over,
    // so scene-entry positioning is never fought by a deliberately persistent request.
    private bool PresentationInfluencesFraming => presentationActive && presentationFramingResolved && startTimer <= 0f;

    private void ResolvePresentationFraming()
    {
        RefreshBaseHalfHeight();

        if (!presentationActive)
        {
            presentationFramingResolved = false;
            presentationFraming = default;
            presentationDesiredZoom = 1f;
            return;
        }

        presentationTargets.RemoveAll(target => target == null);

        if (presentationSettings.RequiresTargets && presentationTargets.Count == 0)
        {
            // GameCameras prunes unresolvable requests on its next Update; until then this frame
            // falls back to ordinary underlying framing rather than a stale focus point.
            presentationFramingResolved = false;
            presentationFraming = default;
            presentationDesiredZoom = 1f;
            return;
        }

        Bounds bounds = ResolveFramedBounds();
        ResolveZoomLimits(out float limitMin, out float limitMax);

        float paddingX = Mathf.Max(0f, presentationPaddingX + Mathf.Max(0f, presentationSettings.paddingX));
        float paddingY = Mathf.Max(0f, presentationPaddingY + Mathf.Max(0f, presentationSettings.paddingY));

        float desiredZoom = presentationSettings.autoZoom
            ? ComputeAutoZoom(bounds, paddingX, paddingY)
            : SanitizeZoom(presentationSettings.authoredZoom);

        float clampedZoom = Mathf.Clamp(desiredZoom, limitMin, limitMax);

        presentationCentre = bounds.center;
        presentationDesiredZoom = clampedZoom;
        presentationFramingResolved = true;
        presentationFraming = new CameraPresentationFraming(
            true,
            presentationId,
            presentationSettings.mode,
            presentationTargets.Count,
            bounds,
            new Vector2(paddingX, paddingY),
            presentationCentre,
            desiredZoom,
            clampedZoom,
            limitMin,
            limitMax,
            !Mathf.Approximately(desiredZoom, clampedZoom),
            presentationFraming.CentreClamped,
            presentationSettings.ResolvedWeight);
    }

    private Bounds ResolveFramedBounds()
    {
        Vector3 offset = new Vector3(presentationSettings.framingOffset.x, presentationSettings.framingOffset.y, 0f);

        if (presentationSettings.mode == CameraPresentationMode.FocusWorldPoint)
        {
            Vector3 point = new Vector3(presentationSettings.worldPoint.x, presentationSettings.worldPoint.y, 0f) + offset;
            return new Bounds(point, Vector3.zero);
        }

        if (presentationSettings.mode == CameraPresentationMode.FocusTarget)
        {
            Vector3 point = FlattenToPlane(presentationTargets[0].position) + offset;
            return new Bounds(point, Vector3.zero);
        }

        Bounds bounds = new Bounds(FlattenToPlane(presentationTargets[0].position), Vector3.zero);
        for (int i = 1; i < presentationTargets.Count; i++)
        {
            bounds.Encapsulate(FlattenToPlane(presentationTargets[i].position));
        }

        bounds.center += offset;
        return bounds;
    }

    private float ComputeAutoZoom(Bounds bounds, float paddingX, float paddingY)
    {
        if (baseHalfHeight <= Mathf.Epsilon)
        {
            return 1f;
        }

        float aspect = cam != null && cam.aspect > Mathf.Epsilon ? cam.aspect : 16f / 9f;
        float requiredHalfHeight = bounds.extents.y + paddingY;
        float requiredHalfWidth = bounds.extents.x + paddingX;

        float zoomForHeight = requiredHalfHeight / baseHalfHeight;
        float zoomForWidth = requiredHalfWidth / (baseHalfHeight * aspect);
        return SanitizeZoom(Mathf.Max(zoomForHeight, zoomForWidth));
    }

    private void ResolveZoomLimits(out float limitMin, out float limitMax)
    {
        limitMin = SanitizeZoom(minZoom);
        limitMax = SanitizeZoom(maxZoom);

        if (presentationSettings.overrideZoomLimits)
        {
            limitMin = SanitizeZoom(presentationSettings.minZoom);
            limitMax = SanitizeZoom(presentationSettings.maxZoom);
        }

        // Room bounds stay authoritative over zoom: the legal-centre region only constrains where
        // the camera may sit, so without this cap a zoom-out could widen the viewport past the
        // authored room and reveal space outside it. The cap never drops below 1, so a room that
        // is already smaller than the authored viewport keeps its existing framing rather than
        // being silently zoomed in.
        limitMax = Mathf.Min(limitMax, ComputeRoomZoomCap());

        if (limitMin > limitMax)
        {
            // Invalid authoring (or a room tighter than the authored minimum) collapses to a
            // single legal zoom rather than producing an empty range; the validator reports the
            // authoring ordering error separately.
            limitMin = limitMax;
        }
    }

    private float ComputeRoomZoomCap()
    {
        CameraBoundsVolume volume = GetActiveBoundsVolume();
        if (volume == null || baseHalfHeight <= Mathf.Epsilon)
        {
            return float.PositiveInfinity;
        }

        float aspect = cam != null && cam.aspect > Mathf.Epsilon ? cam.aspect : 16f / 9f;
        Bounds room = volume.GetBounds();
        float capHeight = room.extents.y / baseHalfHeight;
        float capWidth = room.extents.x / (baseHalfHeight * aspect);
        return Mathf.Max(1f, Mathf.Min(capHeight, capWidth));
    }

    private void UpdateZoom()
    {
        float desired = 1f;
        if (PresentationInfluencesFraming)
        {
            desired = Mathf.LerpUnclamped(1f, presentationDesiredZoom, presentationSettings.ResolvedWeight);
        }

        bool useHysteresis = PresentationInfluencesFraming && presentationSettings.autoZoom;
        if (!useHysteresis)
        {
            targetZoom = desired;
        }
        else
        {
            // Contracting needs a larger change to commit, so a region that shrinks slightly does
            // not pull the camera in and straight back out.
            float band = desired < targetZoom
                ? Mathf.Max(0f, zoomHysteresis + zoomContractHysteresis)
                : Mathf.Max(0f, zoomHysteresis);
            if (Mathf.Abs(desired - targetZoom) > band)
            {
                targetZoom = desired;
            }
        }

        float damp = targetZoom > currentZoom ? zoomOutDampTime : zoomInDampTime;
        float speedLimit = maxZoomSpeed > 0f ? maxZoomSpeed : Mathf.Infinity;
        currentZoom = Mathf.SmoothDamp(
            currentZoom,
            targetZoom,
            ref zoomVelocity,
            Mathf.Max(0f, damp),
            speedLimit,
            Time.deltaTime);

        ApplyZoomToProjection();
    }

    private void ApplyZoomToProjection()
    {
        if (cam == null)
        {
            return;
        }

        currentZoom = SanitizeZoom(currentZoom);

        // Zoom is a pure projection change. Dollying along Z would alter 2.5D layer parallax and
        // fight the enforced cameraZ / frustum-half-extent contract.
        float baseHalfAngle = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
        float zoomedHalfAngle = baseHalfAngle * currentZoom;
        cam.fieldOfView = Mathf.Clamp(2f * Mathf.Atan(zoomedHalfAngle) * Mathf.Rad2Deg, 0.1f, 179f);
    }

    private void ResetZoom()
    {
        currentZoom = 1f;
        targetZoom = 1f;
        zoomVelocity = 0f;
        ApplyZoomToProjection();
    }

    private static float SanitizeZoom(float value)
    {
        return float.IsFinite(value) && value > 0.0001f ? value : 1f;
    }

    private static Vector3 FlattenToPlane(Vector3 worldPosition)
    {
        return new Vector3(worldPosition.x, worldPosition.y, 0f);
    }

    public IReadOnlyList<CameraLockArea> GetRegisteredLockAreasSorted()
    {
        RemoveInvalidLockRegistrations();
        List<LockRegistration> sorted = new List<LockRegistration>(lockRegistrations);
        sorted.Sort((a, b) =>
        {
            int byPriority = b.Priority.CompareTo(a.Priority);
            return byPriority != 0 ? byPriority : b.EntrySequence.CompareTo(a.EntrySequence);
        });

        List<CameraLockArea> areas = new List<CameraLockArea>(sorted.Count);
        for (int i = 0; i < sorted.Count; i++)
        {
            areas.Add(sorted[i].Area);
        }

        return areas;
    }

    public CameraLegalRegion GetLegalRegion()
    {
        ComputeLegalRegion(out AxisInterval legalX, out AxisInterval legalY, out bool xOwned, out bool yOwned);
        return new CameraLegalRegion
        {
            MinX = xOwned ? legalX.Min : float.NegativeInfinity,
            MaxX = xOwned ? legalX.Max : float.PositiveInfinity,
            MinY = yOwned ? legalY.Min : float.NegativeInfinity,
            MaxY = yOwned ? legalY.Max : float.PositiveInfinity,
            XConstrained = xOwned,
            YConstrained = yOwned
        };
    }

    private void RefreshActiveLock(CameraLockArea previousArea)
    {
        CameraLockArea newArea = CurrentLockArea;
        RefreshResolvedMode();
        RefreshTargetMode();

        if (AreLiveTransitionsAllowed())
        {
            CameraTransitionCause cause;
            CameraLockArea overrideProvider;
            if (previousArea == null && newArea != null)
            {
                cause = CameraTransitionCause.FollowToLock;
                overrideProvider = newArea;
            }
            else if (previousArea != null && newArea != null && previousArea != newArea)
            {
                cause = CameraTransitionCause.LockToLock;
                overrideProvider = newArea;
            }
            else if (previousArea != null && newArea == null)
            {
                cause = CameraTransitionCause.LockToFollow;
                overrideProvider = previousArea;
            }
            else
            {
                cause = CameraTransitionCause.None;
                overrideProvider = null;
            }

            if (cause != CameraTransitionCause.None)
            {
                BeginTransition(cause, ResolveTransitionSettings(cause, overrideProvider), previousArea, newArea);
            }
        }

        if (startTimer > 0f)
        {
            SnapToTarget();
        }
    }

    private bool AreLiveTransitionsAllowed()
    {
        return startTimer <= 0f && !positioningOverride && !freezeOverride && !freeOverride;
    }

    private CameraTransitionSettings ResolvePresentationTransitionSettings(
        CameraTransitionCause cause,
        in CameraPresentationSettings settings)
    {
        if (settings.overrideBlend)
        {
            return cause == CameraTransitionCause.PresentationReleased
                ? settings.blendOut
                : settings.blendIn;
        }

        switch (cause)
        {
            case CameraTransitionCause.PresentationReleased: return presentationReleaseTransition;
            case CameraTransitionCause.PresentationChanged: return presentationChangeTransition;
            default: return presentationEnterTransition;
        }
    }

    private CameraTransitionSettings ResolveTransitionSettings(CameraTransitionCause cause, CameraLockArea overrideProvider)
    {
        if (overrideProvider != null && overrideProvider.UseTransitionOverride)
        {
            return cause == CameraTransitionCause.LockToFollow
                ? overrideProvider.ExitTransitionOverride
                : overrideProvider.EntryTransitionOverride;
        }

        switch (cause)
        {
            case CameraTransitionCause.FollowToLock: return followToLockTransition;
            case CameraTransitionCause.LockToLock: return lockToLockTransition;
            case CameraTransitionCause.LockToFollow: return lockToFollowTransition;
            case CameraTransitionCause.OverrideReleased: return overrideReleasedTransition;
            default: return sceneStartTransition;
        }
    }

    private void BeginTransition(CameraTransitionCause cause, CameraTransitionSettings settings, CameraLockArea source, CameraLockArea destination)
    {
        transitionCause = cause;
        transitionSourceLock = source;
        transitionDestinationLock = destination;
        lastApplicationWasImmediate = settings.applyImmediate;

        if (settings.applyImmediate)
        {
            EndTransition();
            SnapToTarget();
            return;
        }

        transitionElapsed = 0f;
        transitionBlendDuration = Mathf.Max(0f, settings.blendDuration);
        transitionStartDampX = settings.dampTimeX;
        transitionStartDampY = settings.dampTimeY;
        currentDampX = settings.dampTimeX;
        currentDampY = settings.dampTimeY;
        transitionActive = transitionBlendDuration > 0f;

        if (settings.resetVelocity)
        {
            velocityX = Vector3.zero;
            velocityY = Vector3.zero;
        }
    }

    private void BeginOverrideReleaseTransition()
    {
        if (startTimer > 0f)
        {
            return;
        }

        CameraLockArea current = CurrentLockArea;
        BeginTransition(
            CameraTransitionCause.OverrideReleased,
            ResolveTransitionSettings(CameraTransitionCause.OverrideReleased, null),
            current,
            current);
    }

    private void EndTransition()
    {
        transitionActive = false;
        transitionCause = CameraTransitionCause.None;
        transitionElapsed = 0f;
        transitionBlendDuration = 0f;
        transitionSourceLock = null;
        transitionDestinationLock = null;
    }

    private void EnforceProjection()
    {
        if (cam == null)
        {
            return;
        }

        cam.orthographic = false;
        cam.fieldOfView = fieldOfView;
        cam.nearClipPlane = config != null ? config.nearClipPlane : 0.3f;
        cam.farClipPlane = config != null ? config.farClipPlane : 1000f;

        Vector3 p = transform.localPosition;
        p.z = cameraZ;
        transform.localPosition = p;

        RefreshBaseHalfHeight();
        ResetZoom();
    }

    // World half-height of the authored (zoom = 1) viewport at the gameplay plane. Derived from
    // the same distance GetFrustumHalfExtents/CameraInfoCache use, so zoom stays consistent with
    // the legal-region inset math.
    private void RefreshBaseHalfHeight()
    {
        baseHalfHeight = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Abs(transform.position.z);
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    private void GetFrustumHalfExtents(out float halfW, out float halfH)
    {
        float dist = Mathf.Abs(transform.position.z);
        halfH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist;
        halfW = halfH * cam.aspect;
    }

    private void ClampToLegalRegion(ref Vector3 dest)
    {
        ComputeLegalRegion(out AxisInterval legalX, out AxisInterval legalY, out bool xOwned, out bool yOwned);
        CameraLockArea area = CurrentLockArea;

        if (xOwned)
        {
            dest.x = Mathf.Clamp(dest.x, legalX.Min, legalX.Max);
        }

        if (!yOwned)
        {
            return;
        }

        if (area != null && area.LockY && lookOffset > 0f && area.PreventLookUp)
        {
            dest.y = Mathf.Min(dest.y, area.HasLookYMax ? area.LookYMax : legalY.Max);
        }
        else if (area != null && area.LockY && lookOffset < 0f && area.PreventLookDown)
        {
            dest.y = Mathf.Max(dest.y, area.HasLookYMin ? area.LookYMin : legalY.Min);
        }

        dest.y = Mathf.Clamp(dest.y, legalY.Min, legalY.Max);
    }

    private void ComputeLegalRegion(out AxisInterval legalX, out AxisInterval legalY, out bool xOwned, out bool yOwned)
    {
        CameraBoundsVolume boundsVolume = GetActiveBoundsVolume();
        GetFrustumHalfExtents(out float halfW, out float halfH);
        CameraLockArea area = CurrentLockArea;
        bool hasRoom = boundsVolume != null;
        bool hasLock = area != null;

        AxisInterval roomX = default;
        AxisInterval roomY = default;
        if (hasRoom)
        {
            Bounds room = boundsVolume.GetBounds();
            roomX = Inset(room.min.x, room.max.x, halfW);
            roomY = Inset(room.min.y, room.max.y, halfH);
        }

        AxisInterval lockX = default;
        AxisInterval lockY = default;
        if (hasLock)
        {
            Rect lockRect = area.GetLockRect();
            lockX = Inset(lockRect.xMin, lockRect.xMax, halfW);
            lockY = Inset(lockRect.yMin, lockRect.yMax, halfH);
        }

        xOwned = hasRoom || (hasLock && area.LockX);
        legalX = default;
        if (xOwned)
        {
            legalX = hasRoom ? roomX : lockX;
            if (hasRoom && hasLock && area.LockX)
            {
                legalX = IntersectOrCollapse(roomX, lockX);
            }
        }

        yOwned = hasRoom || (hasLock && area.LockY);
        legalY = default;
        if (yOwned)
        {
            legalY = hasRoom ? roomY : lockY;
            if (hasRoom && hasLock && area.LockY)
            {
                legalY = IntersectOrCollapse(roomY, lockY);
            }
        }
    }

    private CameraBoundsVolume GetActiveBoundsVolume()
    {
        for (int i = boundsStack.Count - 1; i >= 0; i--)
        {
            CameraBoundsVolume volume = boundsStack[i];
            if (volume != null && volume.isActiveAndEnabled)
            {
                return volume;
            }

            boundsStack.RemoveAt(i);
        }

        return null;
    }

    private LockRegistration GetActiveLockRegistration()
    {
        RemoveInvalidLockRegistrations();
        LockRegistration best = null;

        for (int i = 0; i < lockRegistrations.Count; i++)
        {
            LockRegistration candidate = lockRegistrations[i];
            if (best == null
                || candidate.Priority > best.Priority
                || (candidate.Priority == best.Priority
                    && candidate.EntrySequence > best.EntrySequence))
            {
                best = candidate;
            }
        }

        return best;
    }

    private void RefreshResolvedMode()
    {
        if (freezeOverride || positioningOverride)
        {
            SetMode(CameraMode.Frozen);
        }
        else if (freeOverride)
        {
            SetMode(CameraMode.Free);
        }
        else
        {
            SetMode(CurrentLockArea != null ? CameraMode.Locked : CameraMode.Follow);
        }
    }

    private void RefreshTargetMode()
    {
        if (cameraTarget == null)
        {
            return;
        }

        if (freeOverride)
        {
            cameraTarget.StartFreeMode(true);
            return;
        }

        if (freezeOverride && freezeTargetOverride)
        {
            cameraTarget.FreezeInPlace();
            return;
        }

        CameraLockArea area = CurrentLockArea;
        if (area == null)
        {
            cameraTarget.ExitLockZone();
            return;
        }

        Rect rect = area.GetLockRect();
        float distanceToRect = GetDistanceToRect(cameraTarget.transform.position, rect);
        cameraTarget.EnterLockZone(distanceToRect);
    }

    private LockRegistration FindRegistration(CameraLockArea area)
    {
        for (int i = 0; i < lockRegistrations.Count; i++)
        {
            if (lockRegistrations[i].Area == area)
            {
                return lockRegistrations[i];
            }
        }

        return null;
    }

    private bool RemoveInvalidLockRegistrations()
    {
        return lockRegistrations.RemoveAll(registration =>
            registration == null
            || registration.Area == null
            || !registration.Area.isActiveAndEnabled) > 0;
    }

    private void EnsureSequenceCapacity()
    {
        if (nextLockEntrySequence < long.MaxValue)
        {
            return;
        }

        lockRegistrations.Sort((a, b) => a.EntrySequence.CompareTo(b.EntrySequence));
        for (int i = 0; i < lockRegistrations.Count; i++)
        {
            lockRegistrations[i].EntrySequence = i + 1L;
        }

        nextLockEntrySequence = lockRegistrations.Count;
    }

    private static AxisInterval Inset(float min, float max, float halfExtent)
    {
        float insetMin = min + halfExtent;
        float insetMax = max - halfExtent;
        if (insetMin <= insetMax)
        {
            return new AxisInterval(insetMin, insetMax);
        }

        float center = (min + max) * 0.5f;
        return new AxisInterval(center, center);
    }

    private static AxisInterval IntersectOrCollapse(AxisInterval room, AxisInterval cameraLock)
    {
        float min = Mathf.Max(room.Min, cameraLock.Min);
        float max = Mathf.Min(room.Max, cameraLock.Max);
        if (min <= max)
        {
            return new AxisInterval(min, max);
        }

        float lockCenter = (cameraLock.Min + cameraLock.Max) * 0.5f;
        float nearestRoomPoint = Mathf.Clamp(lockCenter, room.Min, room.Max);
        return new AxisInterval(nearestRoomPoint, nearestRoomPoint);
    }

    private float GetGroundedDampTimeY()
    {
        return config != null ? config.cameraDampTimeYGrounded : dampTimeNormal;
    }

    private void ApplyConfig()
    {
        if (config == null)
        {
            return;
        }

        dampTimeNormal = config.cameraDampTimeNormal;
        dampTimeSlow = config.cameraDampTimeSlow;
        maxVelocity = config.maxVelocity;
        fieldOfView = config.fieldOfView;
        cameraZ = config.cameraZ;
        startLockedTimer = config.startLockedTimer;
        sceneStartTransition = config.sceneStartTransition;
        followToLockTransition = config.followToLockTransition;
        lockToLockTransition = config.lockToLockTransition;
        lockToFollowTransition = config.lockToFollowTransition;
        overrideReleasedTransition = config.overrideReleasedTransition;
        presentationEnterTransition = config.presentationEnterTransition;
        presentationChangeTransition = config.presentationChangeTransition;
        presentationReleaseTransition = config.presentationReleaseTransition;
        presentationPaddingX = config.presentationPaddingX;
        presentationPaddingY = config.presentationPaddingY;
        minZoom = config.minZoom;
        maxZoom = config.maxZoom;
        zoomOutDampTime = config.zoomOutDampTime;
        zoomInDampTime = config.zoomInDampTime;
        maxZoomSpeed = config.maxZoomSpeed;
        zoomHysteresis = config.zoomHysteresis;
        zoomContractHysteresis = config.zoomContractHysteresis;
        presentationCentreDampTime = config.presentationCentreDampTime;
    }

    public void SetMode(CameraMode mode)
    {
        if (Mode == mode)
        {
            return;
        }

        Mode = mode;
        CameraEventService.RaiseModeChanged(mode);
    }

    private static float GetDistanceToRect(Vector3 position, Rect rect)
    {
        float x = Mathf.Clamp(position.x, rect.xMin, rect.xMax);
        float y = Mathf.Clamp(position.y, rect.yMin, rect.yMax);
        return Vector2.Distance(position, new Vector2(x, y));
    }

#if UNITY_EDITOR
    // Selected-only so the Scene view is not permanently cluttered. Everything drawn here is
    // read from already-resolved runtime state; nothing is recomputed or mutated.
    private void OnDrawGizmosSelected()
    {
        if (!drawPresentationGizmos || !Application.isPlaying || !presentationFraming.IsActive)
        {
            return;
        }

        const float z = 0f;
        Bounds framed = presentationFraming.FramedBounds;
        Vector2 padding = presentationFraming.Padding;

        Gizmos.color = new Color(1f, 0.35f, 0.85f, 0.95f);
        DrawWireRect(
            new Rect(framed.min.x, framed.min.y, Mathf.Max(0f, framed.size.x), Mathf.Max(0f, framed.size.y)),
            z);

        Gizmos.color = new Color(1f, 0.35f, 0.85f, 0.4f);
        DrawWireRect(
            new Rect(
                framed.min.x - padding.x,
                framed.min.y - padding.y,
                Mathf.Max(0f, framed.size.x) + padding.x * 2f,
                Mathf.Max(0f, framed.size.y) + padding.y * 2f),
            z);

        Vector3 desiredCentre = new Vector3(presentationFraming.DesiredCentre.x, presentationFraming.DesiredCentre.y, z);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);
        Gizmos.DrawWireSphere(desiredCentre, 0.35f);

        CameraLegalRegion region = GetLegalRegion();
        if (region.XConstrained && region.YConstrained)
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.95f);
            DrawWireRect(
                new Rect(region.MinX, region.MinY, region.MaxX - region.MinX, region.MaxY - region.MinY),
                z);
        }

        // Requested viewport (at the clamped desired zoom) versus the rendered one, so an
        // authored request that is being limited by min/max zoom is visible at a glance.
        float requestedHalfH = baseHalfHeight * presentationFraming.ClampedZoom;
        float aspect = cam != null && cam.aspect > Mathf.Epsilon ? cam.aspect : 16f / 9f;
        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.8f);
        DrawWireRect(CentredRect(desiredCentre, requestedHalfH * aspect, requestedHalfH), z);

        GetFrustumHalfExtents(out float renderedHalfW, out float renderedHalfH);
        Vector3 rendered = transform.position;
        Gizmos.color = new Color(1f, 1f, 1f, 0.7f);
        DrawWireRect(CentredRect(rendered, renderedHalfW, renderedHalfH), z);

        string targets = presentationFraming.ValidTargetCount > 0
            ? BuildTargetLabel()
            : "(no valid targets)";
        UnityEditor.Handles.Label(
            desiredCentre + Vector3.up * (framed.extents.y + padding.y + 0.6f),
            $"Presentation #{presentationFraming.RequestId} {presentationFraming.Mode} P{presentationPriority}"
            + $"\nsource '{presentationSourceLabel}' | weight {presentationFraming.Weight:0.##}"
            + $"\nzoom desired {presentationFraming.DesiredZoom:0.###} -> clamped {presentationFraming.ClampedZoom:0.###}"
            + $" (limits {presentationFraming.MinZoom:0.##}..{presentationFraming.MaxZoom:0.##}"
            + $"{(presentationFraming.ZoomClamped ? ", CLAMPED" : "")}) | current {currentZoom:0.###}"
            + $"\ncentre{(presentationFraming.CentreClamped ? " CLAMPED by legal region" : "")}"
            + $"\ntargets: {targets}");
    }

    private string BuildTargetLabel()
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < presentationTargets.Count; i++)
        {
            if (presentationTargets[i] == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(presentationTargets[i].name);
        }

        return builder.Length > 0 ? builder.ToString() : "(none)";
    }

    private static Rect CentredRect(Vector3 centre, float halfWidth, float halfHeight)
    {
        return new Rect(centre.x - halfWidth, centre.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
    }

    private static void DrawWireRect(Rect rect, float z)
    {
        Vector3 bl = new Vector3(rect.xMin, rect.yMin, z);
        Vector3 br = new Vector3(rect.xMax, rect.yMin, z);
        Vector3 tr = new Vector3(rect.xMax, rect.yMax, z);
        Vector3 tl = new Vector3(rect.xMin, rect.yMax, z);
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }
#endif
}
