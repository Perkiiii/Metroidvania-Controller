using UnityEngine;

// Stores tuning for progression-gated traversal abilities (dash, wall-slide, wall-jump, double-jump).
// Unlock flags live in PlayerAbilityState. Core movement/combat tuning lives in HeroConfig.
// Spirit Cast should use a separate spell/cast config when implemented.
[CreateAssetMenu(menuName = "Hero/Hero Ability Config", fileName = "HeroAbilityConfig")]
public sealed class HeroAbilityConfig : ScriptableObject
{
    [Header("Dash")]
    public float dashSpeed = 18f;
    public float dashDuration = 0.22f;
    public float dashCooldown = 0.45f;

    [Header("Wall Slide")]
    public float wallSlideInitialHoldTime = 0.25f;
    public float wallSlideInitialSpeed = 0.1f;
    public float wallSlideAcceleration = 16f;
    public float wallSlideSpeed = -3f;
    public float wallSlideInputThreshold = 0.3f;

    [Header("Wall Jump")]
    public float wallJumpHorizontalSpeed = 10f;
    public float wallJumpVerticalSpeed = 16f;
    public float wallJumpRelatchLockout = 0.35f;

    [Header("Double Jump")]
    public float doubleJumpSpeed = 14f;
    public bool resetDoubleJumpOnWallSlide = false;
}
