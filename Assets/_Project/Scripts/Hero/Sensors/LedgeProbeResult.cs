using UnityEngine;

public readonly struct LedgeProbeResult
{
    public Collider2D PrimaryCollider { get; }
    public Collider2D SecondarySupportCollider { get; }
    public Rigidbody2D TargetBody { get; }
    public Transform TargetFrame { get; }
    public Vector2 LocalSurfacePoint { get; }
    public Vector2 LocalCatchPosition { get; }
    public Vector2 LocalCrestPosition { get; }
    public Vector2 LocalStandingPosition { get; }
    public int Direction { get; }

    public bool HasSecondarySupport => SecondarySupportCollider != null;

    public LedgeProbeResult(
        Collider2D primaryCollider,
        Collider2D secondarySupportCollider,
        Rigidbody2D targetBody,
        Transform targetFrame,
        Vector2 surfacePoint,
        Vector2 catchPosition,
        Vector2 crestPosition,
        Vector2 standingPosition,
        int direction)
    {
        PrimaryCollider = primaryCollider;
        SecondarySupportCollider = secondarySupportCollider;
        TargetBody = targetBody;
        TargetFrame = targetFrame;
        LocalSurfacePoint = ToLocal(targetFrame, surfacePoint);
        LocalCatchPosition = ToLocal(targetFrame, catchPosition);
        LocalCrestPosition = ToLocal(targetFrame, crestPosition);
        LocalStandingPosition = ToLocal(targetFrame, standingPosition);
        Direction = direction >= 0 ? 1 : -1;
    }

    public Vector2 ResolveSurfacePoint() => ToWorld(TargetFrame, LocalSurfacePoint);
    public Vector2 ResolveCatchPosition() => ToWorld(TargetFrame, LocalCatchPosition);
    public Vector2 ResolveCrestPosition() => ToWorld(TargetFrame, LocalCrestPosition);
    public Vector2 ResolveStandingPosition() => ToWorld(TargetFrame, LocalStandingPosition);

    public bool HasUsableTarget()
    {
        return TargetFrame != null
            && IsUsable(PrimaryCollider)
            && (!HasSecondarySupport || IsUsable(SecondarySupportCollider));
    }

    private static bool IsUsable(Collider2D collider)
    {
        return collider != null
            && collider.enabled
            && collider.gameObject.activeInHierarchy;
    }

    private static Vector2 ToLocal(Transform frame, Vector2 worldPoint)
    {
        return frame != null ? (Vector2)frame.InverseTransformPoint(worldPoint) : worldPoint;
    }

    private static Vector2 ToWorld(Transform frame, Vector2 localPoint)
    {
        return frame != null ? (Vector2)frame.TransformPoint(localPoint) : localPoint;
    }
}
