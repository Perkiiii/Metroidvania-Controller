using UnityEngine;

// Read-only diagnostics view of one registered presentation request.
public readonly struct CameraPresentationSnapshot
{
    public CameraPresentationSnapshot(
        long id,
        int priority,
        CameraPresentationMode mode,
        string sourceLabel,
        CameraRequestLifetime lifetime,
        float remainingSeconds,
        int validTargetCount,
        string targetLabel,
        bool isSelected,
        float weight)
    {
        Id = id;
        Priority = priority;
        Mode = mode;
        SourceLabel = sourceLabel ?? "(none)";
        Lifetime = lifetime;
        RemainingSeconds = remainingSeconds;
        ValidTargetCount = validTargetCount;
        TargetLabel = targetLabel ?? "";
        IsSelected = isSelected;
        Weight = weight;
    }

    public long Id { get; }
    public int Priority { get; }
    public CameraPresentationMode Mode { get; }
    public string SourceLabel { get; }
    public CameraRequestLifetime Lifetime { get; }

    // -1 when the request has no independent duration.
    public float RemainingSeconds { get; }
    public int ValidTargetCount { get; }
    public string TargetLabel { get; }
    public bool IsSelected { get; }
    public float Weight { get; }
}

// Read-only diagnostics view of the framing the controller resolved for the selected request.
public readonly struct CameraPresentationFraming
{
    public CameraPresentationFraming(
        bool isActive,
        long requestId,
        CameraPresentationMode mode,
        int validTargetCount,
        Bounds framedBounds,
        Vector2 padding,
        Vector3 desiredCentre,
        float desiredZoom,
        float clampedZoom,
        float minZoom,
        float maxZoom,
        bool zoomClamped,
        bool centreClamped,
        float weight)
    {
        IsActive = isActive;
        RequestId = requestId;
        Mode = mode;
        ValidTargetCount = validTargetCount;
        FramedBounds = framedBounds;
        Padding = padding;
        DesiredCentre = desiredCentre;
        DesiredZoom = desiredZoom;
        ClampedZoom = clampedZoom;
        MinZoom = minZoom;
        MaxZoom = maxZoom;
        ZoomClamped = zoomClamped;
        CentreClamped = centreClamped;
        Weight = weight;
    }

    public bool IsActive { get; }
    public long RequestId { get; }
    public CameraPresentationMode Mode { get; }
    public int ValidTargetCount { get; }

    // The target region before padding, in world space.
    public Bounds FramedBounds { get; }
    public Vector2 Padding { get; }
    public Vector3 DesiredCentre { get; }
    public float DesiredZoom { get; }
    public float ClampedZoom { get; }
    public float MinZoom { get; }
    public float MaxZoom { get; }
    public bool ZoomClamped { get; }
    public bool CentreClamped { get; }
    public float Weight { get; }
}
