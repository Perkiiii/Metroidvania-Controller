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

    [Header("Gravity & Fall")]
    public float baseGravityScale = 0.79f;
    public float riseGravityScale = 0.79f;
    public float apexGravityScale = 0.79f;
    public float fallGravityScale = 0.79f;
    public bool normalizeGravityToReference = true;
    public float referenceGravityY = -29.8f;
    public float apexVelocityThreshold = 1f;
    public float maxFallSpeed = 15f;

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
    [Tooltip("Duration of time-scale freeze on the first confirmed enemy hit per swing.")]
    [Min(0f)] public float attackHitStopDuration = 0.06f;
    [Tooltip("Duration of time-scale freeze on the first confirmed clash per swing (shorter than a hit).")]
    [Min(0f)] public float attackClashHitStopDuration = 0.04f;
    [Min(0f)]
    [Tooltip("Upward velocity applied when an airborne downslash successfully hits a valid enemy.")]
    public float downslashBounceVelocity = 16f;

    [Header("Sensors")]
    public LayerMask terrainLayers = 1;
    public float groundProbeDistance = 0.16f;
    public float ceilingProbeDistance = 0.08f;
    public float wallProbeDistance = 0.1f;
    public float sensorInset = 0.02f;

    [Header("Ledge Climb")]
    [Tooltip("Static surface layers eligible to become a ledge. Keep this narrower than terrainLayers.")]
    public LayerMask ledgeSurfaceLayers = 1 << 7;
    [Min(0f)] public float ledgeMaxUpwardSpeed = 5f;
    [Min(0f)] public float ledgeMinimumHeightFromFeet = 0.25f;
    [Min(0f)] public float ledgeMaximumHeightFromFeet = 1.35f;
    [Min(0f)] public float ledgeTopProbeExtraHeight = 0.3f;
    [Min(0f)] public float ledgeTopSampleInset = 0.04f;
    [Min(0f)] public float ledgeSurfaceHeightTolerance = 0.08f;
    [Range(0f, 1f)] public float ledgeMinimumUpNormal = 0.85f;
    [Min(0f)] public float ledgeSupportGapTolerance = 0.08f;
    [Min(0f)] public float ledgePlacementSkin = 0.02f;
    [Min(0f)] public float ledgeCatchDrop = 0.1f;
    [Min(0f)] public float ledgePreCatchGraceDuration = 0.13f;
    [Min(0.01f)] public float ledgeCatchDuration = 0.08f;
    [Min(0.01f)] public float ledgePullUpDuration = 0.28f;
    [Min(0.01f)] public float ledgeSettleDuration = 0.05f;

    [Header("Health")]
    [Tooltip("Legacy serialized value. PlayerHealthState is the authoritative maximum-health owner.")]
    [System.Obsolete("Use PlayerHealthState.MaximumHealth. This field remains serialized for migration safety.")]
    public int maxHealth = 5;
    public float iFrameDuration = 1.5f;

    [Header("Hurt Response")]
    public float hurtStunDuration = 0.35f;
    public float hurtKnockbackX = 8f;
    public float hurtKnockbackY = 4f;
    public float deathRespawnFallbackDelay = 2.0f;

    [Header("Animation")]
    public float locomotionFadeDuration = 0.08f;
    public float airFadeDuration = 0.05f;
    public float actionFadeDuration = 0.03f;
    public float locomotionIdleThreshold = 0f;
    [Tooltip("Should match walkSpeed in Movement.")]
    public float locomotionWalkThreshold = 4.32f;
    [Tooltip("Should match runSpeed in Movement.")]
    public float locomotionRunThreshold = 6.5f;

    [Header("Audio")]
    [Min(0f)] public float footstepMinSpeed = 0.1f;
}
