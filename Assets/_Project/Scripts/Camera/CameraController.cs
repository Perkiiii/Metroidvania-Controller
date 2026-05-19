using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class CameraController : MonoBehaviour
{
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
    public CameraLockArea CurrentLockArea => GetActiveLockArea();
    public float LookInputThreshold => config != null ? config.lookInputThreshold : 0.5f;
    public float ManualLookHoldDelay => config != null ? config.manualLookHoldDelay : 2f;

    private readonly List<CameraBoundsVolume> boundsStack = new List<CameraBoundsVolume>();
    private readonly List<CameraLockArea> lockStack = new List<CameraLockArea>();

    private Camera cam;
    private Coroutine positioningRoutine;
    private Vector3 velocityX;
    private Vector3 velocityY;
    private float currentDampX;
    private float currentDampY;
    private float lookSlowTimer;
    private float startTimer;
    private float lookOffsetTarget;

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
        ApplyConfig();
        EnforceProjection();
        boundsStack.Clear();
        lockStack.Clear();
        SetMode(CameraMode.Follow);
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;
        currentDampX = dampTimeNormal;
        currentDampY = GetGroundedDampTimeY();
        lookOffset = 0f;
        lookOffsetTarget = 0f;
        lookSlowTimer = 0f;
        ResetStartTimer();

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
        if (area == null || lockStack.Contains(area))
        {
            return;
        }

        lockStack.Add(area);
        RefreshActiveLock();
    }

    public void ExitLockArea(CameraLockArea area)
    {
        if (area == null || !lockStack.Remove(area))
        {
            return;
        }

        RefreshActiveLock();
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
        SetMode(CameraMode.Frozen);
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;

        if (freezeTarget)
        {
            cameraTarget?.FreezeInPlace();
        }
    }

    public void StopFreeze(bool stopFreezeTarget = false)
    {
        SetMode(lockStack.Count > 0 ? CameraMode.Locked : CameraMode.Follow);

        if (stopFreezeTarget)
        {
            if (lockStack.Count > 0)
            {
                RefreshActiveLock();
            }
            else
            {
                cameraTarget?.EndFreeMode();
            }
        }
    }

    public void StartFreeMode(bool keepTargetHorizontalOffset = true)
    {
        SetMode(CameraMode.Free);
        velocityX = Vector3.zero;
        velocityY = Vector3.zero;
        cameraTarget?.StartFreeMode(keepTargetHorizontalOffset);
    }

    public void EndFreeMode()
    {
        SetMode(lockStack.Count > 0 ? CameraMode.Locked : CameraMode.Follow);
        cameraTarget?.EndFreeMode();
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
        CameraMode previousMode = Mode;

        if (freezeTarget)
        {
            cameraTarget?.FreezeInPlace();
        }

        yield return new WaitForFixedUpdate();

        cameraTarget?.SnapToHero();
        SnapToTarget();
        FreezeInPlace(false);

        float settleTime = config != null ? config.positioningSettleTime : 0.1f;
        if (settleTime > 0f)
        {
            yield return new WaitForSeconds(settleTime);
        }

        SetMode(previousMode == CameraMode.Frozen
            ? (lockStack.Count > 0 ? CameraMode.Locked : CameraMode.Follow)
            : previousMode);
        CameraInfoCache.UpdateCache(cam, true);

        if (freezeTarget)
        {
            if (lockStack.Count > 0)
            {
                RefreshActiveLock();
            }
            else
            {
                cameraTarget?.EndFreeMode();
            }
        }

        IsPositioningCamera = false;
        positioningRoutine = null;
        PositionedAtHero?.Invoke();
    }

    private void UpdateDampTimes()
    {
        currentDampX = Mathf.MoveTowards(currentDampX, dampTimeNormal, 0.007f);

        float targetDampY;
        if (lookSlowTimer > 0f)
        {
            lookSlowTimer -= Time.deltaTime;
            targetDampY = config != null ? config.lookInputDampTimeY : 0.35f;
        }
        else if (cameraTarget.IsFastFalling)
        {
            targetDampY = config != null ? config.cameraDampTimeYFastFalling : 0.12f;
        }
        else if (cameraTarget.IsFalling)
        {
            targetDampY = config != null ? config.cameraDampTimeYFalling : 0.18f;
        }
        else if (cameraTarget.IsRising)
        {
            targetDampY = config != null ? config.cameraDampTimeYRising : 0.28f;
        }
        else
        {
            targetDampY = GetGroundedDampTimeY();
        }

        float minDamp = config != null ? config.cameraDampTimeYMin : 0.03f;
        currentDampY = Mathf.MoveTowards(currentDampY, Mathf.Max(minDamp, targetDampY), Time.deltaTime);
    }

    private Vector3 ComputeDestination()
    {
        float z = transform.position.z;
        Vector3 dest = new Vector3(
            cameraTarget.transform.position.x,
            cameraTarget.transform.position.y + cameraTarget.CurrentVerticalOffset + lookOffset,
            z);

        ClampToBounds(ref dest);
        ClampToLockArea(ref dest);
        return dest;
    }

    private void RefreshActiveLock()
    {
        CameraLockArea area = GetActiveLockArea();
        if (area == null)
        {
            SetMode(CameraMode.Follow);
            cameraTarget?.ExitLockZone();
            currentDampX = dampTimeSlow;
            currentDampY = dampTimeSlow;
            return;
        }

        SetMode(CameraMode.Locked);
        Rect rect = area.GetLockRect();
        float distanceToRect = GetDistanceToRect(cameraTarget != null ? cameraTarget.transform.position : transform.position, rect);
        cameraTarget?.EnterLockZone(rect, distanceToRect);
        currentDampX = dampTimeSlow;
        currentDampY = dampTimeSlow;

        if (startTimer > 0f)
        {
            SnapToTarget();
        }
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

    private void ClampToBounds(ref Vector3 dest)
    {
        CameraBoundsVolume boundsVolume = GetActiveBoundsVolume();
        if (boundsVolume == null)
        {
            return;
        }

        GetFrustumHalfExtents(out float halfW, out float halfH);
        Bounds b = boundsVolume.GetBounds();

        dest.x = ClampOrCenter(dest.x, b.min.x + halfW, b.max.x - halfW);
        dest.y = ClampOrCenter(dest.y, b.min.y + halfH, b.max.y - halfH);
    }

    private void ClampToLockArea(ref Vector3 dest)
    {
        CameraLockArea area = GetActiveLockArea();
        if (area == null)
        {
            return;
        }

        GetFrustumHalfExtents(out float halfW, out float halfH);
        Rect r = area.GetLockRect();

        if (area.LockX)
        {
            dest.x = ClampOrCenter(dest.x, r.xMin + halfW, r.xMax - halfW);
        }

        if (!area.LockY)
        {
            return;
        }

        float minY = r.yMin + halfH;
        float maxY = r.yMax - halfH;

        if (lookOffset > 0f && area.PreventLookUp)
        {
            dest.y = Mathf.Min(dest.y, area.HasLookYMax ? area.LookYMax : maxY);
        }
        else if (lookOffset < 0f && area.PreventLookDown)
        {
            dest.y = Mathf.Max(dest.y, area.HasLookYMin ? area.LookYMin : minY);
        }

        dest.y = ClampOrCenter(dest.y, minY, maxY);
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

    private CameraLockArea GetActiveLockArea()
    {
        CameraLockArea best = null;

        for (int i = lockStack.Count - 1; i >= 0; i--)
        {
            CameraLockArea area = lockStack[i];
            if (area == null || !area.isActiveAndEnabled)
            {
                lockStack.RemoveAt(i);
                continue;
            }

            if (best == null || area.Priority > best.Priority)
            {
                best = area;
            }
        }

        return best;
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

    private static float ClampOrCenter(float value, float min, float max)
    {
        return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }

    private static float GetDistanceToRect(Vector3 position, Rect rect)
    {
        float x = Mathf.Clamp(position.x, rect.xMin, rect.xMax);
        float y = Mathf.Clamp(position.y, rect.yMin, rect.yMax);
        return Vector2.Distance(position, new Vector2(x, y));
    }
}
