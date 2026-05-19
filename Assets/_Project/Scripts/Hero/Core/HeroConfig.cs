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
    public float jumpCutVelocityMultiplier = 0.35f;
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
    public float attackCooldown = 0.4f;
    public float attackRecovery = 0.7f;
    public float altAttackResetTime = 1f;
    public int attackDamage = 1;
    [Min(1)] public int maxHitsPerSwing = 16;
    public float attackDirectionThreshold = 0.5f;
    [Range(0f, 1f)] public float groundAttackMoveMultiplier = 0.75f;
    public float attackBufferTime = 0.1f;
    public LayerMask attackHitLayers;
    [Tooltip("Terrain layers checked for whiff sparks during the active attack window. Falls back to terrainLayers if zero.")]
    public LayerMask attackTerrainLayers;

    [Header("Attack Impact")]
    [Tooltip("Duration of time-scale freeze on the first confirmed enemy hit per swing.")]
    [Min(0f)] public float attackHitStopDuration = 0.06f;
    [Tooltip("Duration of time-scale freeze on the first confirmed clash per swing (shorter than a hit).")]
    [Min(0f)] public float attackClashHitStopDuration = 0.04f;

    [Header("Downslash Bounce")]
    [Min(0f)]
    [Tooltip("Upward velocity applied when an airborne downslash successfully hits a valid enemy.")]
    public float downslashBounceVelocity = 16f;

    [Header("Wall Slide")]
    public float wallSlideSpeed = -3f;
    public float wallSlideDeceleration = 0.7f;
    public float wallSlideInputThreshold = 0.3f;
    public float wallSlideInitialHoldTime = 0.25f;
    public float wallSlideInitialSpeed = -0.5f;
    public float wallSlideAcceleration = 12f;
    public float wallJumpHorizontalSpeed = 10f;
    public float wallJumpVerticalSpeed = 16f;
    public float wallJumpRelatchLockout = 0.15f;

    [Header("Sensors")]
    public LayerMask terrainLayers = 1;
    public float groundProbeDistance = 0.16f;
    public float ceilingProbeDistance = 0.08f;
    public float wallProbeDistance = 0.1f;
    public float sensorInset = 0.02f;

    [Header("Health & Hurt")]
    public int maxHealth = 5;
    public float iFrameDuration = 1.5f;
    public float hurtStunDuration = 0.35f;
    public float hurtKnockbackX = 8f;
    public float hurtKnockbackY = 4f;

    [Header("Animation")]
    public float locomotionFadeDuration = 0.08f;
    public float airFadeDuration = 0.05f;
    public float actionFadeDuration = 0.03f;
    public float locomotionIdleThreshold = 0f;
    public float locomotionWalkThreshold = 4.32f;
    public float locomotionRunThreshold = 6.5f;

    [Header("Audio")]
    [Min(0f)] public float footstepMinSpeed = 0.1f;
}
