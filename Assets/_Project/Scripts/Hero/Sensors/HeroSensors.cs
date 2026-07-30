using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroSensors : MonoBehaviour
{
    private readonly RaycastHit2D[] solidHitBuffer = new RaycastHit2D[1];
    private readonly RaycastHit2D[] ledgeHitBuffer = new RaycastHit2D[8];
    private readonly Collider2D[] overlapBuffer = new Collider2D[16];

    private HeroConfig config;
    private HeroStateBlackboard blackboard;
    private Rigidbody2D body;
    private Collider2D bodyCollider;

    public bool IsGrounded { get; private set; }
    public bool IsTouchingCeiling { get; private set; }
    public bool IsTouchingFrontWall { get; private set; }
    public bool IsTouchingBackWall { get; private set; }
    public LedgeProbeFailure LastLedgeFailure { get; private set; }

    public void Initialize(
        HeroConfig heroConfig,
        HeroStateBlackboard stateBlackboard,
        Rigidbody2D rigidbody,
        Collider2D collider)
    {
        config = heroConfig;
        blackboard = stateBlackboard;
        body = rigidbody;
        bodyCollider = collider;
    }

    public void FixedTick()
    {
        if (config == null || blackboard == null || bodyCollider == null)
        {
            return;
        }

        IsGrounded = ProbeGround();
        IsTouchingCeiling = ProbeCeiling();
        IsTouchingFrontWall = ProbeWall(blackboard.FacingDirection);
        IsTouchingBackWall = ProbeWall(-blackboard.FacingDirection);

        blackboard.SetGrounded(IsGrounded);
        blackboard.touchingCeiling = IsTouchingCeiling;
        blackboard.touchingWallFront = IsTouchingFrontWall;
        blackboard.touchingWallBack = IsTouchingBackWall;
    }

    private bool ProbeGround()
    {
        Bounds bounds = bodyCollider.bounds;
        float inset = config.sensorInset;
        float y = bounds.center.y;
        Vector2 left = new Vector2(bounds.min.x + inset, y);
        Vector2 center = bounds.center;
        Vector2 right = new Vector2(bounds.max.x - inset, y);
        float distance = bounds.extents.y + config.groundProbeDistance;

        return RayHitsSolid(left, Vector2.down, distance)
            || RayHitsSolid(center, Vector2.down, distance)
            || RayHitsSolid(right, Vector2.down, distance);
    }

    private bool ProbeCeiling()
    {
        Bounds bounds = bodyCollider.bounds;
        float inset = config.sensorInset;
        float y = bounds.max.y - inset;
        Vector2 left = new Vector2(bounds.min.x + inset, y);
        Vector2 center = new Vector2(bounds.center.x, y);
        Vector2 right = new Vector2(bounds.max.x - inset, y);

        return RayHitsSolid(left, Vector2.up, config.ceilingProbeDistance)
            || RayHitsSolid(center, Vector2.up, config.ceilingProbeDistance)
            || RayHitsSolid(right, Vector2.up, config.ceilingProbeDistance);
    }

    private bool ProbeWall(int direction)
    {
        Bounds bounds = bodyCollider.bounds;
        float x = direction > 0 ? bounds.max.x : bounds.min.x;
        Vector2 rayDirection = direction > 0 ? Vector2.right : Vector2.left;
        float inset = config.sensorInset;

        Vector2 lower = new Vector2(x, bounds.min.y + inset);
        Vector2 center = new Vector2(x, bounds.center.y);
        Vector2 upper = new Vector2(x, bounds.max.y - inset);

        return RayHitsSolid(lower, rayDirection, config.wallProbeDistance)
            || RayHitsSolid(center, rayDirection, config.wallProbeDistance)
            || RayHitsSolid(upper, rayDirection, config.wallProbeDistance);
    }

    private bool RayHitsSolid(Vector2 origin, Vector2 direction, float distance)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(config.terrainLayers);
        filter.useTriggers = false;
        return Physics2D.Raycast(origin, direction, filter, solidHitBuffer, distance) > 0;
    }

    public bool TryFindLedge(int direction, out LedgeProbeResult result)
    {
        result = default;
        LastLedgeFailure = LedgeProbeFailure.None;

        if (config == null || body == null || bodyCollider == null)
        {
            return Fail(LedgeProbeFailure.NotInitialized);
        }

        direction = direction >= 0 ? 1 : -1;
        Bounds bounds = bodyCollider.bounds;
        if (!TryFindFrontWall(bounds, direction, out RaycastHit2D wallHit))
        {
            return LastLedgeFailure != LedgeProbeFailure.None
                ? false
                : Fail(LedgeProbeFailure.NoFrontWall);
        }

        float feetY = bounds.min.y;
        float probeStartY = feetY + config.ledgeMaximumHeightFromFeet + config.ledgeTopProbeExtraHeight;
        float probeDistance = config.ledgeMaximumHeightFromFeet
            - config.ledgeMinimumHeightFromFeet
            + config.ledgeTopProbeExtraHeight * 2f;
        float halfWidth = bounds.extents.x;
        float edgeX = wallHit.point.x;
        float nearX = edgeX + direction * Mathf.Max(config.ledgeTopSampleInset, config.ledgePlacementSkin);
        float farX = edgeX + direction * Mathf.Max(
            halfWidth * 2f + config.ledgePlacementSkin - config.ledgeTopSampleInset,
            config.ledgeTopSampleInset * 2f);

        if (!TryRaycastEligibleSurface(new Vector2(nearX, probeStartY), probeDistance, out RaycastHit2D nearHit)
            || !TryRaycastEligibleSurface(new Vector2(farX, probeStartY), probeDistance, out RaycastHit2D farHit))
        {
            return false;
        }

        float surfaceY = (nearHit.point.y + farHit.point.y) * 0.5f;
        float ledgeHeight = surfaceY - feetY;
        if (ledgeHeight < config.ledgeMinimumHeightFromFeet
            || ledgeHeight > config.ledgeMaximumHeightFromFeet)
        {
            return Fail(LedgeProbeFailure.HeightOutOfRange);
        }

        if (nearHit.normal.y < config.ledgeMinimumUpNormal
            || farHit.normal.y < config.ledgeMinimumUpNormal)
        {
            return Fail(LedgeProbeFailure.SurfaceTooSteep);
        }

        if (Mathf.Abs(nearHit.point.y - farHit.point.y) > config.ledgeSurfaceHeightTolerance)
        {
            return Fail(LedgeProbeFailure.SurfaceHeightMismatch);
        }

        Collider2D primary = NormalizeComposite(nearHit.collider);
        Collider2D secondary = NormalizeComposite(farHit.collider);
        if (!ValidateSurfacePair(primary, secondary))
        {
            return false;
        }

        if (secondary == primary)
        {
            secondary = null;
        }

        float centerOffsetX = bounds.center.x - body.position.x;
        float centerOffsetY = bounds.center.y - body.position.y;
        float feetOffset = body.position.y - bounds.min.y;
        float topOffset = bounds.max.y - body.position.y;
        float outsideBodyX = edgeX - direction * (halfWidth + config.ledgePlacementSkin) - centerOffsetX;
        float standingBodyX = edgeX + direction * (halfWidth + config.ledgePlacementSkin) - centerOffsetX;
        float standingBodyY = surfaceY + feetOffset + config.ledgePlacementSkin;
        Vector2 catchPosition = new Vector2(
            outsideBodyX,
            surfaceY - topOffset - config.ledgeCatchDrop);
        Vector2 crestPosition = new Vector2(outsideBodyX, standingBodyY);
        Vector2 standingPosition = new Vector2(standingBodyX, standingBodyY);

        if (!ValidateContinuousSupport(standingPosition, surfaceY, primary, secondary))
        {
            return false;
        }

        if (!IsBodyClear(catchPosition))
        {
            return Fail(LedgeProbeFailure.CatchBlocked);
        }

        if (!IsBodyClear(crestPosition))
        {
            return Fail(LedgeProbeFailure.CrestBlocked);
        }

        if (!IsBodyClear(standingPosition))
        {
            return Fail(LedgeProbeFailure.StandingBlocked);
        }

        if (!IsCorridorClear(catchPosition, crestPosition)
            || !IsCorridorClear(crestPosition, standingPosition))
        {
            return Fail(LedgeProbeFailure.CorridorBlocked);
        }

        if (IsRestricted(catchPosition)
            || IsRestricted(crestPosition)
            || IsRestricted(standingPosition)
            || IsRestricted(nearHit.point)
            || IsRestricted(farHit.point))
        {
            return Fail(LedgeProbeFailure.RestrictedVolume);
        }

        Collider2D target = primary;
        Transform targetFrame = target != null ? target.transform : null;
        Rigidbody2D targetBody = target != null ? target.attachedRigidbody : null;
        if (targetFrame == null)
        {
            return Fail(LedgeProbeFailure.TargetInvalid);
        }

        result = new LedgeProbeResult(
            target,
            secondary,
            targetBody,
            targetFrame,
            nearHit.point,
            catchPosition,
            crestPosition,
            standingPosition,
            direction);
        return true;
    }

    public bool ValidateLedgePath(in LedgeProbeResult result)
    {
        if (!result.HasUsableTarget())
        {
            return Fail(LedgeProbeFailure.TargetInvalid);
        }

        if (result.TargetBody != null
            && !IsRequiredStaticCompositeBody(result.PrimaryCollider, result.TargetBody))
        {
            return Fail(LedgeProbeFailure.UnsupportedRigidbodySurface);
        }

        if (HasUnsupportedAttachedBody(result.PrimaryCollider)
            || (result.HasSecondarySupport && HasUnsupportedAttachedBody(result.SecondarySupportCollider)))
        {
            return Fail(LedgeProbeFailure.UnsupportedRigidbodySurface);
        }

        Vector2 catchPosition = result.ResolveCatchPosition();
        Vector2 crestPosition = result.ResolveCrestPosition();
        Vector2 standingPosition = result.ResolveStandingPosition();
        float surfaceY = result.ResolveSurfacePoint().y;
        if (!ValidateContinuousSupport(
                standingPosition,
                surfaceY,
                result.PrimaryCollider,
                result.SecondarySupportCollider))
        {
            return false;
        }

        if (!IsBodyClear(catchPosition)
            || !IsBodyClear(crestPosition)
            || !IsBodyClear(standingPosition)
            || !IsCorridorClear(catchPosition, crestPosition)
            || !IsCorridorClear(crestPosition, standingPosition))
        {
            return Fail(LedgeProbeFailure.CorridorBlocked);
        }

        if (IsRestricted(catchPosition)
            || IsRestricted(crestPosition)
            || IsRestricted(standingPosition)
            || IsRestricted(result.ResolveSurfacePoint()))
        {
            return Fail(LedgeProbeFailure.RestrictedVolume);
        }

        LastLedgeFailure = LedgeProbeFailure.None;
        return true;
    }

    private bool TryFindFrontWall(Bounds bounds, int direction, out RaycastHit2D hit)
    {
        Vector2 rayDirection = direction > 0 ? Vector2.right : Vector2.left;
        float x = direction > 0 ? bounds.max.x : bounds.min.x;
        float inset = Mathf.Max(0f, config.sensorInset);
        Vector2[] origins =
        {
            new Vector2(x, bounds.min.y + inset),
            new Vector2(x, bounds.center.y),
            new Vector2(x, bounds.max.y - inset)
        };

        for (int i = 0; i < origins.Length; i++)
        {
            if (TryRaycast(origins[i], rayDirection, config.wallProbeDistance, config.terrainLayers, out hit))
            {
                if (ValidateEligibleSurface(hit.collider))
                {
                    return true;
                }

                hit = default;
                return false;
            }
        }

        hit = default;
        return false;
    }

    private bool TryRaycastEligibleSurface(Vector2 origin, float distance, out RaycastHit2D hit)
    {
        if (!TryRaycast(origin, Vector2.down, distance, config.terrainLayers, out hit))
        {
            return FailWithHit(LedgeProbeFailure.NoTopSurface, out hit);
        }

        if (!ValidateEligibleSurface(hit.collider))
        {
            hit = default;
            return false;
        }

        return true;
    }

    private bool ValidateEligibleSurface(Collider2D collider)
    {
        if (collider == null)
        {
            return Fail(LedgeProbeFailure.NoTopSurface);
        }

        if (collider.isTrigger)
        {
            return Fail(LedgeProbeFailure.TriggerSurface);
        }

        if ((config.ledgeSurfaceLayers.value & (1 << collider.gameObject.layer)) == 0)
        {
            return Fail(LedgeProbeFailure.UnsupportedLayer);
        }

        Collider2D normalized = NormalizeComposite(collider);
        if (HasUnsupportedAttachedBody(collider)
            || (normalized != collider && HasUnsupportedAttachedBody(normalized)))
        {
            return Fail(LedgeProbeFailure.UnsupportedRigidbodySurface);
        }

        return true;
    }

    private bool ValidateSurfacePair(Collider2D primary, Collider2D secondary)
    {
        if (!ValidateEligibleSurface(primary) || !ValidateEligibleSurface(secondary))
        {
            return false;
        }

        if (primary == secondary)
        {
            return true;
        }

        if (primary.composite != null && primary.composite == secondary.composite)
        {
            return true;
        }

        if (HasUnsupportedAttachedBody(primary) || HasUnsupportedAttachedBody(secondary))
        {
            return Fail(LedgeProbeFailure.UnsupportedRigidbodySurface);
        }

        return true;
    }

    private bool ValidateContinuousSupport(
        Vector2 standingBodyPosition,
        float expectedSurfaceY,
        Collider2D primary,
        Collider2D secondary)
    {
        Bounds bounds = bodyCollider.bounds;
        float halfWidth = Mathf.Max(0f, bounds.extents.x - config.ledgeTopSampleInset);
        float maxStep = Mathf.Max(0.01f, config.ledgeSupportGapTolerance);
        int sampleCount = Mathf.Clamp(Mathf.CeilToInt((halfWidth * 2f) / maxStep) + 1, 3, 9);
        float centerOffsetX = bounds.center.x - body.position.x;
        float centerX = standingBodyPosition.x + centerOffsetX;
        float rayStartY = expectedSurfaceY + config.ledgeSurfaceHeightTolerance + config.ledgePlacementSkin;
        float rayDistance = config.ledgeSurfaceHeightTolerance * 2f + config.ledgePlacementSkin * 2f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 0.5f : i / (float)(sampleCount - 1);
            float x = Mathf.Lerp(centerX - halfWidth, centerX + halfWidth, t);
            if (!TryRaycast(
                    new Vector2(x, rayStartY),
                    Vector2.down,
                    rayDistance,
                    config.ledgeSurfaceLayers,
                    out RaycastHit2D supportHit))
            {
                if (!TryValidateBoundedSeamSupport(
                        x,
                        rayStartY,
                        rayDistance,
                        expectedSurfaceY,
                        primary,
                        secondary))
                {
                    return Fail(LedgeProbeFailure.UnsupportedGap);
                }

                continue;
            }

            Collider2D support = NormalizeComposite(supportHit.collider);
            if (!ValidateEligibleSurface(support)
                || supportHit.normal.y < config.ledgeMinimumUpNormal
                || Mathf.Abs(supportHit.point.y - expectedSurfaceY) > config.ledgeSurfaceHeightTolerance)
            {
                return Fail(LedgeProbeFailure.UnsupportedGap);
            }

            if (support != primary && support != secondary)
            {
                return Fail(LedgeProbeFailure.AmbiguousGeometry);
            }
        }

        return true;
    }

    private bool TryValidateBoundedSeamSupport(
        float sampleX,
        float rayStartY,
        float rayDistance,
        float expectedSurfaceY,
        Collider2D primary,
        Collider2D secondary)
    {
        if (secondary == null || config.ledgeSupportGapTolerance <= 0f)
        {
            return false;
        }

        float offset = config.ledgeSupportGapTolerance;
        if (!TryRaycast(
                new Vector2(sampleX - offset, rayStartY),
                Vector2.down,
                rayDistance,
                config.ledgeSurfaceLayers,
                out RaycastHit2D leftHit)
            || !TryRaycast(
                new Vector2(sampleX + offset, rayStartY),
                Vector2.down,
                rayDistance,
                config.ledgeSurfaceLayers,
                out RaycastHit2D rightHit))
        {
            return false;
        }

        Collider2D leftSupport = NormalizeComposite(leftHit.collider);
        Collider2D rightSupport = NormalizeComposite(rightHit.collider);
        if (leftSupport == rightSupport
            || (leftSupport != primary && leftSupport != secondary)
            || (rightSupport != primary && rightSupport != secondary)
            || !ValidateEligibleSurface(leftSupport)
            || !ValidateEligibleSurface(rightSupport)
            || leftHit.normal.y < config.ledgeMinimumUpNormal
            || rightHit.normal.y < config.ledgeMinimumUpNormal
            || Mathf.Abs(leftHit.point.y - expectedSurfaceY) > config.ledgeSurfaceHeightTolerance
            || Mathf.Abs(rightHit.point.y - expectedSurfaceY) > config.ledgeSurfaceHeightTolerance)
        {
            return false;
        }

        Collider2D leftCollider = leftHit.point.x <= rightHit.point.x ? leftSupport : rightSupport;
        Collider2D rightCollider = leftCollider == leftSupport ? rightSupport : leftSupport;
        float measuredGap = rightCollider.bounds.min.x - leftCollider.bounds.max.x;
        return measuredGap >= 0f && measuredGap <= config.ledgeSupportGapTolerance;
    }

    private bool IsBodyClear(Vector2 bodyPosition)
    {
        Bounds bounds = bodyCollider.bounds;
        Vector2 centerOffset = (Vector2)bounds.center - body.position;
        Vector2 size = (Vector2)bounds.size - Vector2.one * (config.ledgePlacementSkin * 2f);
        size.x = Mathf.Max(0.01f, size.x);
        size.y = Mathf.Max(0.01f, size.y);
        ContactFilter2D filter = CreateFilter(config.terrainLayers, false);
        int count = Physics2D.OverlapBox(bodyPosition + centerOffset, size, 0f, filter, overlapBuffer);
        return count == 0;
    }

    private bool IsCorridorClear(Vector2 fromBodyPosition, Vector2 toBodyPosition)
    {
        const int Samples = 5;
        for (int i = 1; i <= Samples; i++)
        {
            if (!IsBodyClear(Vector2.Lerp(fromBodyPosition, toBodyPosition, i / (float)Samples)))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsRestricted(Vector2 bodyOrSurfacePosition)
    {
        Bounds bounds = bodyCollider.bounds;
        Vector2 size = new Vector2(
            Mathf.Max(config.ledgePlacementSkin * 2f, bounds.size.x * 0.9f),
            Mathf.Max(config.ledgePlacementSkin * 2f, bounds.size.y * 0.25f));
        ContactFilter2D filter = CreateFilter(Physics2D.AllLayers, true);
        int count = Physics2D.OverlapBox(bodyOrSurfacePosition, size, 0f, filter, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D overlap = overlapBuffer[i];
            if (overlap == null || !overlap.enabled || !overlap.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (overlap.TryGetComponent(out NoLedgeClimbVolume volume) && volume.enabled)
            {
                return true;
            }

            if (overlap.TryGetComponent(out HazardZone hazard) && hazard.enabled)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryRaycast(
        Vector2 origin,
        Vector2 direction,
        float distance,
        LayerMask layers,
        out RaycastHit2D hit)
    {
        ContactFilter2D filter = CreateFilter(layers, false);
        int count = Physics2D.Raycast(origin, direction, filter, ledgeHitBuffer, distance);
        if (count > 0)
        {
            hit = ledgeHitBuffer[0];
            return true;
        }

        hit = default;
        return false;
    }

    private static ContactFilter2D CreateFilter(LayerMask layers, bool useTriggers)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(layers);
        filter.useTriggers = useTriggers;
        return filter;
    }

    private static Collider2D NormalizeComposite(Collider2D collider)
    {
        return collider != null && collider.composite != null ? collider.composite : collider;
    }

    private static bool HasUnsupportedAttachedBody(Collider2D collider)
    {
        if (collider == null || collider.attachedRigidbody == null)
        {
            return false;
        }

        return !IsRequiredStaticCompositeBody(collider, collider.attachedRigidbody);
    }

    private static bool IsRequiredStaticCompositeBody(Collider2D collider, Rigidbody2D attachedBody)
    {
        return collider is CompositeCollider2D
            && attachedBody != null
            && attachedBody.bodyType == RigidbodyType2D.Static;
    }

    private bool Fail(LedgeProbeFailure failure)
    {
        LastLedgeFailure = failure;
        return false;
    }

    private bool FailWithHit(LedgeProbeFailure failure, out RaycastHit2D hit)
    {
        hit = default;
        return Fail(failure);
    }
}
