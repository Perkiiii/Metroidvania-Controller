using System;
using UnityEngine;

public enum CameraFadeDirection
{
    In,
    Out
}

public enum CameraFreezeKind
{
    Soft,
    Hard
}

public enum CameraRequestLifetime
{
    Scene,
    Persistent
}

public readonly struct CameraRequestHandle : IDisposable
{
    private readonly GameCameras owner;
    private readonly long requestId;

    internal CameraRequestHandle(GameCameras owner, long requestId)
    {
        this.owner = owner;
        this.requestId = requestId;
    }

    public bool IsValid => owner != null && requestId != 0L;

    public void Release()
    {
        owner?.ReleaseFreeze(requestId);
    }

    public void Dispose()
    {
        Release();
    }
}

public readonly struct CameraFadeRequest
{
    public CameraFadeRequest(CameraFadeDirection direction, float duration)
    {
        Direction = direction;
        Duration = duration;
    }

    public CameraFadeDirection Direction { get; }
    public float Duration { get; }
}

public readonly struct CameraShakeRequest
{
    public CameraShakeRequest(CameraShakeIntensity intensity, Vector2 worldPosition, float intensityMultiplier, object source)
    {
        Intensity = intensity;
        WorldPosition = worldPosition;
        IntensityMultiplier = intensityMultiplier;
        Source = source;
        Profile = null;
    }

    public CameraShakeRequest(CameraShakeProfile profile, Vector2 worldPosition, float intensityMultiplier, object source)
    {
        Profile = profile;
        WorldPosition = worldPosition;
        IntensityMultiplier = intensityMultiplier;
        Source = source;
        Intensity = profile != null ? profile.Intensity : CameraShakeIntensity.Small;
    }

    public CameraShakeIntensity Intensity { get; }
    public CameraShakeProfile Profile { get; }
    public Vector2 WorldPosition { get; }
    public float IntensityMultiplier { get; }
    public object Source { get; }
}

public static class CameraEventService
{
    public static event Action<CameraLockArea> LockEntered;
    public static event Action<CameraLockArea> LockExited;
    public static event Action<CameraOffsetArea> OffsetEntered;
    public static event Action<CameraOffsetArea> OffsetExited;
    public static event Action<CameraFadeRequest> FadeRequested;
    public static event Action<CameraShakeRequest> ShakeRequested;
    public static event Action<object> ShakeCancelRequested;
    public static event Action<CameraMode> ModeChanged;

    public static void RaiseLockEntered(CameraLockArea area) => LockEntered?.Invoke(area);
    public static void RaiseLockExited(CameraLockArea area) => LockExited?.Invoke(area);
    public static void RaiseOffsetEntered(CameraOffsetArea area) => OffsetEntered?.Invoke(area);
    public static void RaiseOffsetExited(CameraOffsetArea area) => OffsetExited?.Invoke(area);
    public static void RequestFade(CameraFadeDirection direction, float duration = -1f) => FadeRequested?.Invoke(new CameraFadeRequest(direction, duration));
    public static void RequestShake(CameraShakeIntensity intensity, Vector2 worldPosition, float intensityMultiplier = 1f, object source = null) => ShakeRequested?.Invoke(new CameraShakeRequest(intensity, worldPosition, intensityMultiplier, source));
    public static void RequestShake(CameraShakeProfile profile, Vector2 worldPosition, float intensityMultiplier = 1f, object source = null) => ShakeRequested?.Invoke(new CameraShakeRequest(profile, worldPosition, intensityMultiplier, source));
    public static void RequestShakeCancel(object source = null) => ShakeCancelRequested?.Invoke(source);
    public static CameraRequestHandle AcquireFreeze(
        CameraFreezeKind kind,
        float duration = -1f,
        object source = null,
        CameraRequestLifetime lifetime = CameraRequestLifetime.Scene)
    {
        return GameCameras.Instance != null
            ? GameCameras.Instance.AcquireFreeze(kind, duration, source, lifetime)
            : default;
    }
    public static CameraPresentationHandle AcquirePresentation(
        in CameraPresentationSettings settings,
        Transform[] targets = null,
        object source = null,
        CameraRequestLifetime lifetime = CameraRequestLifetime.Scene,
        int priority = 0,
        float duration = -1f)
    {
        return GameCameras.Instance != null
            ? GameCameras.Instance.AcquirePresentation(settings, targets, source, lifetime, priority, duration)
            : default;
    }

    public static void RaiseModeChanged(CameraMode mode) => ModeChanged?.Invoke(mode);
}
