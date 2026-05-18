using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroStateBlackboard : MonoBehaviour
{
    [Header("Actor")]
    public HeroActorState actorState = HeroActorState.Airborne;
    public bool controlLocked;
    public bool inputBlocked;

    [Header("Ground")]
    public bool grounded;
    public bool wasGrounded;
    public bool touchingCeiling;

    [Header("Air")]
    public bool jumping;
    public bool jumpSustaining;
    public bool rising;
    public bool falling;

    [Header("Movement")]
    public bool moving;
    public bool facingRight = true;
    public float desiredMoveX;
    public Vector2 velocity;

    [Header("Walls")]
    public bool touchingWallFront;
    public bool touchingWallBack;

    [Header("Future Actions")]
    public bool dashing;
    public bool attacking;
    public bool attackRecovering;
    public bool upAttacking;
    public bool downAttacking;
    public bool altAttack;
    public float altAttackTime;
    public bool wallSliding;
    public bool wallJumping;
    public bool recoiling;
    public HeroAttackDirection attackDirection = HeroAttackDirection.Side;

    [Header("Debug")]
    public int jumpStepsElapsed;
    public int jumpedSteps;

    public int FacingDirection => facingRight ? 1 : -1;

    public void SetGrounded(bool isGrounded)
    {
        wasGrounded = grounded;
        grounded = isGrounded;
        if (actorState == HeroActorState.Hurt || actorState == HeroActorState.Dead)
        {
            return;
        }
        actorState = grounded ? HeroActorState.Grounded : HeroActorState.Airborne;
    }
}
