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
        bool wasFrozen = freezeOverride;
        freezeOverride = frozen;
        freezeTargetOverride = frozen && freezeTarget;
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;
        RefreshResolvedMode();
        RefreshTargetMode();

        if (wasFrozen && !frozen && !freeOverride)
        {
            BeginOverrideReleaseTransition();
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

        transform.position = ComputeDestination();
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;
        CameraInfoCache.UpdateCache(cam, true);
        EndTransition();
        lastApplicationWasImmediate = true;
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

        if (Mode == CameraMode.Frozen || Mode == CameraMode.Free || Time.timeScale <= Mathf.Epsilon)
        {
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
        float targetDampY = Mathf.Max(minDamp, ResolveStateDampY());

        if (transitionActive)
        {
            transitionElapsed += Time.deltaTime;
            float t = transitionBlendDuration > 0f ? Mathf.Clamp01(transitionElapsed / transitionBlendDuration) : 1f;
            currentDampX = Mathf.Lerp(transitionStartDampX, dampTimeNormal, t);
            currentDampY = Mathf.Lerp(transitionStartDampY, targetDampY, t);

            if (t >= 1f)
            {
                EndTransition();
            }

            return;
        }

        currentDampX = Mathf.MoveTowards(currentDampX, dampTimeNormal, 0.007f);
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
        Vector3 dest = new Vector3(
            cameraTarget.transform.position.x,
            cameraTarget.transform.position.y + cameraTarget.CurrentVerticalOffset + lookOffset,
            z);

        ClampToLegalRegion(ref dest);
        lastComputedDestination = dest;
        return dest;
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

        bool liveTransitionsAllowed = startTimer <= 0f && !positioningOverride && !freezeOverride && !freeOverride;
        if (liveTransitionsAllowed)
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
}
