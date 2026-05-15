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
    Hard,
    Release
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

public readonly struct CameraFreezeRequest
{
    public CameraFreezeRequest(CameraFreezeKind kind, float duration, object source)
    {
        Kind = kind;
        Duration = duration;
        Source = source;
    }

    public CameraFreezeKind Kind { get; }
    public float Duration { get; }
    public object Source { get; }
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
    public static event Action<CameraFreezeRequest> FreezeRequested;
    public static event Action<CameraMode> ModeChanged;

    public static void RaiseLockEntered(CameraLockArea area) => LockEntered?.Invoke(area);
    public static void RaiseLockExited(CameraLockArea area) => LockExited?.Invoke(area);
    public static void RaiseOffsetEntered(CameraOffsetArea area) => OffsetEntered?.Invoke(area);
    public static void RaiseOffsetExited(CameraOffsetArea area) => OffsetExited?.Invoke(area);
    public static void RequestFade(CameraFadeDirection direction, float duration = -1f) => FadeRequested?.Invoke(new CameraFadeRequest(direction, duration));
    public static void RequestShake(CameraShakeIntensity intensity, Vector2 worldPosition, float intensityMultiplier = 1f, object source = null) => ShakeRequested?.Invoke(new CameraShakeRequest(intensity, worldPosition, intensityMultiplier, source));
    public static void RequestShake(CameraShakeProfile profile, Vector2 worldPosition, float intensityMultiplier = 1f, object source = null) => ShakeRequested?.Invoke(new CameraShakeRequest(profile, worldPosition, intensityMultiplier, source));
    public static void RequestShakeCancel(object source = null) => ShakeCancelRequested?.Invoke(source);
    public static void RequestFreeze(CameraFreezeKind kind, float duration = -1f, object source = null) => FreezeRequested?.Invoke(new CameraFreezeRequest(kind, duration, source));
    public static void RaiseModeChanged(CameraMode mode) => ModeChanged?.Invoke(mode);
}
