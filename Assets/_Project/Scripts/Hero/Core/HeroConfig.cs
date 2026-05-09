using UnityEngine;

[CreateAssetMenu(menuName = "Hero/Hero Config", fileName = "HeroConfig")]
public sealed class HeroConfig : ScriptableObject
{
    [Header("Movement")]
    public float walkSpeed = 4.32f;
    public float runSpeed = 6.5f;
    public float groundAcceleration = 80f;
    public float groundDeceleration = 90f;
    public float airAcceleration = 60f;
    public float airDeceleration = 40f;
    public float horizontalInputDeadZone = 0.3f;
    public bool requireSprintForRun;

    [Header("Jump")]
    public float jumpSpeed = 18f;
    public int maxJumpSustainSteps = 1;
    public int minJumpReleaseSteps;
    public float coyoteTime = 0.08f;
    public float jumpBufferTime = 0.1f;

    [Header("Gravity")]
    public float baseGravityScale = 0.79f;
    public float riseGravityScale = 0.79f;
    public float apexGravityScale = 0.79f;
    public float fallGravityScale = 0.79f;
    public bool normalizeGravityToReference = true;
    public float referenceGravityY = -29.8f;
    public float apexVelocityThreshold = 1f;
    public float maxFallSpeed = 15f;

    [Header("Dash")]
    public float dashSpeed = 18f;
    public float dashDuration = 0.4f;
    public float dashCooldown = 0.86f;

    [Header("Attack")]
    public float attackDuration = 0.3f;
    public float attackCooldown = 0.4f;
    public float attackRecovery = 0.7f;
    public int attackDamage = 1;
    public float attackDirectionThreshold = 0.5f;
    public float attackHitboxStartTime = 0.05f;
    public float attackHitboxEndTime = 0.16f;
    public LayerMask attackHitLayers;
    public Vector2 attackSideOffset = new Vector2(0.75f, 0f);
    public Vector2 attackSideSize = new Vector2(1.2f, 0.5f);
    public Vector2 attackUpOffset = new Vector2(0f, 0.75f);
    public Vector2 attackUpSize = new Vector2(0.75f, 1f);
    public Vector2 attackDownOffset = new Vector2(0f, -0.75f);
    public Vector2 attackDownSize = new Vector2(0.75f, 1f);

    [Header("Wall Slide")]
    public float wallSlideSpeed = -3f;
    public float wallSlideDeceleration = 0.7f;
    public float wallSlideInputThreshold = 0.3f;

    [Header("Sensors")]
    public LayerMask terrainLayers = 1;
    public float groundProbeDistance = 0.16f;
    public float ceilingProbeDistance = 0.08f;
    public float wallProbeDistance = 0.1f;
    public float sensorInset = 0.02f;

    [Header("Animation")]
    public float locomotionFadeDuration = 0.08f;
    public float airFadeDuration = 0.05f;
    public float actionFadeDuration = 0.03f;
    public float locomotionIdleThreshold = 0f;
    public float locomotionWalkThreshold = 4.32f;
    public float locomotionRunThreshold = 6.5f;
}
