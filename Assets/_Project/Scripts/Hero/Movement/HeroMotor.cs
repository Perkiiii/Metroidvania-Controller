using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroMotor : MonoBehaviour
{
    private HeroConfig config;
    private HeroAbilityConfig abilityConfig;
    private HeroStateBlackboard blackboard;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Transform spriteRoot;

    private float desiredMoveX;
    private bool runRequested;
    private int jumpStepsElapsed;
    private int jumpedSteps;
    private bool jumpReleasePending;
    private bool normalMovementSuppressed;
    private bool gravitySuspended;
    private float savedGravityScale;
    private float wallSlideInitialTimer;

    public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

    public void Initialize(
        HeroConfig heroConfig,
        HeroAbilityConfig heroAbilityConfig,
        HeroStateBlackboard stateBlackboard,
        Rigidbody2D rigidbody,
        SpriteRenderer renderer,
        Transform visualRoot)
    {
        config = heroConfig;
        abilityConfig = heroAbilityConfig;
        blackboard = stateBlackboard;
        body = rigidbody;
        spriteRenderer = renderer;
        spriteRoot = visualRoot;

        if (body != null && config != null)
        {
            body.gravityScale = GetEffectiveGravityScale(config.baseGravityScale);
        }
    }

    public void SetDesiredMove(float moveX, bool wantsRun)
    {
        desiredMoveX = Mathf.Clamp(moveX, -1f, 1f);
        runRequested = wantsRun;

        if (blackboard != null)
        {
            blackboard.desiredMoveX = desiredMoveX;
            blackboard.moving = Mathf.Abs(desiredMoveX) > GetDeadZone();
        }
    }

    public void SetFacingDirection(int direction)
    {
        if (blackboard == null || direction == 0)
        {
            return;
        }

        blackboard.facingRight = direction > 0;
        ApplyFacingVisuals();
    }

    public void StartJump()
    {
        if (body == null || config == null || blackboard == null)
        {
            return;
        }

        jumpStepsElapsed = 1;
        jumpedSteps = 1;
        jumpReleasePending = false;

        blackboard.jumping = true;
        blackboard.jumpSustaining = true;
        blackboard.grounded = false;
        blackboard.actorState = HeroActorState.Airborne;
        blackboard.rising = true;
        blackboard.falling = false;
        blackboard.jumpStepsElapsed = jumpStepsElapsed;
        blackboard.jumpedSteps = jumpedSteps;

        SetVerticalVelocity(config.jumpSpeed);
    }

    public void SustainJump()
    {
        if (body == null || config == null || blackboard == null || !blackboard.jumpSustaining)
        {
            return;
        }

        if (jumpStepsElapsed <= config.maxJumpSustainSteps)
        {
            SetVerticalVelocity(config.jumpSpeed);
            jumpStepsElapsed++;
            jumpedSteps++;
            blackboard.jumpStepsElapsed = jumpStepsElapsed;
            blackboard.jumpedSteps = jumpedSteps;

            if (jumpReleasePending)
            {
                TryCutJump();
            }

            return;
        }

        StopJumpSustain();
    }

    public void RequestJumpRelease()
    {
        jumpReleasePending = true;
        TryCutJump();
    }

    public void StopJumpSustain()
    {
        if (blackboard == null)
        {
            return;
        }

        blackboard.jumping = false;
        blackboard.jumpSustaining = false;
        jumpReleasePending = false;
        jumpStepsElapsed = 0;
        blackboard.jumpStepsElapsed = jumpStepsElapsed;
    }

    public void SetNormalMovementSuppressed(bool suppressed)
    {
        normalMovementSuppressed = suppressed;
    }

    public void SetGravitySuspended(bool suspended)
    {
        if (body == null)
        {
            return;
        }

        if (suspended)
        {
            if (!gravitySuspended)
            {
                savedGravityScale = body.gravityScale;
            }

            gravitySuspended = true;
            body.gravityScale = 0f;
            return;
        }

        if (!gravitySuspended)
        {
            return;
        }

        gravitySuspended = false;
        body.gravityScale = savedGravityScale;
    }

    public void BeginWallSlide()
    {
        wallSlideInitialTimer = abilityConfig != null ? abilityConfig.wallSlideInitialHoldTime : 0f;
    }

    public void EndWallSlide()
    {
        wallSlideInitialTimer = 0f;
    }

    public void ApplyKnockback(Vector2 velocity)
    {
        if (body == null)
        {
            return;
        }

        body.linearVelocity = velocity;
    }

    public void SetDashVelocity(int direction)
    {
        if (body == null || abilityConfig == null)
        {
            return;
        }

        body.linearVelocity = new Vector2(direction * abilityConfig.dashSpeed, 0f);
    }

    public void ApplyDownslashBounce()
    {
        if (body == null || config == null || blackboard == null)
        {
            return;
        }

        ResetJumpRuntime();
        blackboard.wallSliding = false;
        blackboard.wallJumping = false;
        EndWallSlide();
        blackboard.grounded = false;
        blackboard.actorState = HeroActorState.Airborne;
        blackboard.rising = true;
        blackboard.falling = false;

        SetVerticalVelocity(config.downslashBounceVelocity);
    }

    public void StartWallJump(int wallDirection)
    {
        if (body == null || abilityConfig == null || blackboard == null || wallDirection == 0)
        {
            return;
        }

        jumpStepsElapsed = 0;
        jumpedSteps = 0;
        jumpReleasePending = false;
        EndWallSlide();

        blackboard.wallSliding = false;
        blackboard.jumping = true;
        blackboard.jumpSustaining = false;
        blackboard.grounded = false;
        blackboard.actorState = HeroActorState.Airborne;
        blackboard.rising = true;
        blackboard.falling = false;
        blackboard.jumpStepsElapsed = 0;
        blackboard.jumpedSteps = 0;

        int awayDirection = -wallDirection;
        SetFacingDirection(awayDirection);
        body.linearVelocity = new Vector2(
            awayDirection * abilityConfig.wallJumpHorizontalSpeed,
            abilityConfig.wallJumpVerticalSpeed);
    }

    public void StartDoubleJump(float verticalSpeed)
    {
        if (body == null || blackboard == null)
        {
            return;
        }

        jumpStepsElapsed = 1;
        jumpedSteps = 1;
        jumpReleasePending = false;

        blackboard.jumping = true;
        blackboard.jumpSustaining = false;
        blackboard.grounded = false;
        blackboard.actorState = HeroActorState.Airborne;
        blackboard.rising = true;
        blackboard.falling = false;
        blackboard.jumpStepsElapsed = jumpStepsElapsed;
        blackboard.jumpedSteps = jumpedSteps;

        SetVerticalVelocity(verticalSpeed);
    }

    public void ResetJumpRuntime()
    {
        if (blackboard == null)
        {
            return;
        }

        blackboard.jumping = false;
        blackboard.jumpSustaining = false;
        blackboard.rising = false;
        blackboard.falling = false;
        jumpReleasePending = false;
        jumpStepsElapsed = 0;
        jumpedSteps = 0;
        blackboard.jumpStepsElapsed = 0;
        blackboard.jumpedSteps = 0;
    }

    public void FixedTick(float fixedDeltaTime)
    {
        if (body == null || config == null || blackboard == null)
        {
            return;
        }

        if (!normalMovementSuppressed)
        {
            ApplyHorizontalVelocity(fixedDeltaTime);
        }

        if (!gravitySuspended)
        {
            ApplyGravityScale();
        }

        ApplyWallSlideVelocity(fixedDeltaTime);
        ClampFallSpeed();
        ApplyFacingVisuals();

        blackboard.velocity = body.linearVelocity;
        blackboard.rising = body.linearVelocity.y > 0.01f;
        blackboard.falling = !blackboard.grounded && body.linearVelocity.y < -0.01f;
    }

    private void ApplyHorizontalVelocity(float fixedDeltaTime)
    {
        float deadZone = GetDeadZone();
        float input = Mathf.Abs(desiredMoveX) > deadZone ? desiredMoveX : 0f;
        float speed = GetTargetSpeed();
        float targetX = input * speed;
        if (blackboard.attacking && blackboard.grounded && !blackboard.dashing)
        {
            targetX *= config.groundAttackMoveMultiplier;
        }

        Vector2 velocity = body.linearVelocity;
        bool accelerating = Mathf.Abs(targetX) > Mathf.Abs(velocity.x);
        float acceleration = blackboard.grounded
            ? (accelerating ? config.groundAcceleration : config.groundDeceleration)
            : (accelerating ? config.airAcceleration : config.airDeceleration);

        velocity.x = Mathf.MoveTowards(velocity.x, targetX, acceleration * fixedDeltaTime);
        body.linearVelocity = velocity;
    }

    private void ApplyWallSlideVelocity(float fixedDeltaTime)
    {
        if (!blackboard.wallSliding || abilityConfig == null)
        {
            return;
        }

        if (wallSlideInitialTimer > 0f)
        {
            wallSlideInitialTimer -= fixedDeltaTime;
            float targetY = Mathf.Min(0f, abilityConfig.wallSlideInitialSpeed);
            float currentY = body.linearVelocity.y;
            // Cap fast downward contacts so the initial cling is felt even when entering at high fall speed.
            float cappedY = currentY < targetY ? targetY : currentY;
            body.linearVelocity = new Vector2(body.linearVelocity.x, cappedY);
            return;
        }

        float y = Mathf.MoveTowards(
            body.linearVelocity.y,
            abilityConfig.wallSlideSpeed,
            Mathf.Max(0f, abilityConfig.wallSlideAcceleration) * fixedDeltaTime);

        body.linearVelocity = new Vector2(body.linearVelocity.x, y);
    }

    private void ApplyGravityScale()
    {
        if (blackboard.jumpSustaining && body.linearVelocity.y > 0f)
        {
            body.gravityScale = GetEffectiveGravityScale(config.riseGravityScale);
            return;
        }

        float verticalSpeed = Mathf.Abs(body.linearVelocity.y);
        if (!blackboard.grounded && verticalSpeed <= config.apexVelocityThreshold)
        {
            body.gravityScale = GetEffectiveGravityScale(config.apexGravityScale);
        }
        else if (body.linearVelocity.y < 0f)
        {
            body.gravityScale = GetEffectiveGravityScale(config.fallGravityScale);
        }
        else
        {
            body.gravityScale = GetEffectiveGravityScale(config.baseGravityScale);
        }
    }

    private float GetEffectiveGravityScale(float configuredScale)
    {
        if (config == null || !config.normalizeGravityToReference)
        {
            return configuredScale;
        }

        float projectGravityY = Physics2D.gravity.y;
        if (Mathf.Abs(projectGravityY) <= Mathf.Epsilon)
        {
            return configuredScale;
        }

        return configuredScale * Mathf.Abs(config.referenceGravityY / projectGravityY);
    }

    private void ClampFallSpeed()
    {
        if (body.linearVelocity.y >= -config.maxFallSpeed)
        {
            return;
        }

        body.linearVelocity = new Vector2(body.linearVelocity.x, -config.maxFallSpeed);
    }

    private void TryCutJump()
    {
        if (body == null || config == null || blackboard == null)
        {
            return;
        }

        if (body.linearVelocity.y > 0f && jumpedSteps >= config.minJumpReleaseSteps)
        {
            float multiplier = Mathf.Clamp01(config.jumpCutVelocityMultiplier);
            SetVerticalVelocity(body.linearVelocity.y * multiplier);
            StopJumpSustain();
        }
    }

    private void SetVerticalVelocity(float velocityY)
    {
        body.linearVelocity = new Vector2(body.linearVelocity.x, velocityY);
    }

    private float GetTargetSpeed()
    {
        if (config.requireSprintForRun && !runRequested)
        {
            return config.walkSpeed;
        }

        return config.runSpeed;
    }

    private float GetDeadZone()
    {
        return config != null ? config.horizontalInputDeadZone : 0.1f;
    }

    private void ApplyFacingVisuals()
    {
        if (blackboard == null)
        {
            return;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = !blackboard.facingRight;
            return;
        }

        if (spriteRoot == null)
        {
            return;
        }

        Vector3 scale = spriteRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * blackboard.FacingDirection;
        spriteRoot.localScale = scale;
    }
}
