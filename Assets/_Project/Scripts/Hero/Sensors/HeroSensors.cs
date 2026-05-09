using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroSensors : MonoBehaviour
{
    private HeroConfig config;
    private HeroStateBlackboard blackboard;
    private Rigidbody2D body;
    private Collider2D bodyCollider;

    public bool IsGrounded { get; private set; }
    public bool IsTouchingCeiling { get; private set; }
    public bool IsTouchingFrontWall { get; private set; }
    public bool IsTouchingBackWall { get; private set; }

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
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, distance, config.terrainLayers);
        return hit.collider != null && !hit.collider.isTrigger;
    }
}
