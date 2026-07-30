using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroMotor : MonoBehaviour
{
    private HeroConfig config;
    private HeroAbilityConfig abilityConfig;
    private HeroStateBlackboard blackboard;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
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
    private bool ledgeClimbActive;

    private bool scriptedEntryActive;
    private Vector2 scriptedVelocityTarget;
    private bool scriptedLockY;

    public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
    public Vector2 Position => body != null ? body.position : (Vector2)transform.position;

    public void Initialize(
        HeroConfig heroConfig,
        HeroAbilityConfig heroAbilityConfig,
        HeroStateBlackboard stateBlackboard,
        Rigidbody2D rigidbody,
        Collider2D collider,
        SpriteRenderer renderer,
        Transform visualRoot)
    {
        config = heroConfig;
        abilityConfig = heroAbilityConfig;
        blackboard = stateBlackboard;
        body = rigidbody;
        bodyCollider = collider;
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

    // Bind owns the action state; the motor remains the only component that writes
    // the rigidbody while the state requests a stationary hold.
    public void HoldStationary()
    {
        if (body == null)
        {
            return;
        }

        body.linearVelocity = Vector2.zero;
        if (blackboard != null)
        {
            blackboard.velocity = Vector2.zero;
        }
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

    // Nudge the hero out of a gate or trigger by a world-space offset, optionally zeroing
    // one or both velocity components so the hero doesn't immediately drift back in.
    // Caller (TransitionPoint) computes the offset from collider bounds.
    public void PushOut(Vector2 worldOffset, bool zeroVelocityX, bool zeroVelocityY)
    {
        if (body == null)
        {
            return;
        }

        if (zeroVelocityX || zeroVelocityY)
        {
            Vector2 v = body.linearVelocity;
            if (zeroVelocityX) v.x = 0f;
            if (zeroVelocityY) v.y = 0f;
            body.linearVelocity = v;
        }

        body.position += worldOffset;
    }

    public void TeleportTo(Vector2 position, bool resetVelocity = true)
    {
        if (body == null)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            return;
        }

        if (resetVelocity)
        {
            body.linearVelocity = Vector2.zero;
            if (blackboard != null)
                blackboard.velocity = Vector2.zero;
        }

        body.position = position;
        // Rigidbody2D.position only propagates to the Transform on the next physics step, so a
        // same-frame reader of transform.position (e.g. the scene-entry camera readiness that runs
        // immediately after placement, behind the black screen) would otherwise see the stale
        // pre-teleport position and frame the wrong spot. Writing the Transform makes the teleport
        // visible in the same frame; the Transform change is also picked up by the readiness path's
        // Physics2D.SyncTransforms() so the body stays consistent.
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    public Vector2 GetPositionWithFeetAt(Vector2 desiredPosition, float groundY, float skin = 0.02f)
    {
        if (body == null || bodyCollider == null)
        {
            return desiredPosition;
        }

        float feetOffset = body.position.y - bodyCollider.bounds.min.y;
        return new Vector2(desiredPosition.x, groundY + Mathf.Max(0f, skin) + feetOffset);
    }

    public void BeginScriptedEntry(bool zeroGravity)
    {
        scriptedEntryActive = true;
        scriptedVelocityTarget = Vector2.zero;
        scriptedLockY = true;
        SetNormalMovementSuppressed(true);
        if (zeroGravity)
        {
            SetGravitySuspended(true);
        }
        ResetJumpRuntime();
    }

    public void EndScriptedEntry()
    {
        scriptedEntryActive = false;
        scriptedLockY = true;
        scriptedVelocityTarget = Vector2.zero;
        SetGravitySuspended(false);
        SetNormalMovementSuppressed(false);
    }

    public void SetScriptedVelocity(Vector2 velocity)
    {
        scriptedVelocityTarget = velocity;
        scriptedLockY = true;
        if (body != null)
        {
            body.linearVelocity = velocity;
        }
    }

    public void SetScriptedVelocityX(float velocityX)
    {
        scriptedVelocityTarget = new Vector2(velocityX, 0f);
        scriptedLockY = false;
        if (body != null)
        {
            body.linearVelocity = new Vector2(velocityX, body.linearVelocity.y);
        }
    }

    public void SetScriptedVelocityX(float velocityX, float initialVelocityY)
    {
        scriptedVelocityTarget = new Vector2(velocityX, 0f);
        scriptedLockY = false;
        if (body != null)
        {
            body.linearVelocity = new Vector2(velocityX, initialVelocityY);
        }
    }

    public void ResetMotion()
    {
        ledgeClimbActive = false;
        ResetJumpRuntime();
        EndWallSlide();
        SetGravitySuspended(false);
        SetNormalMovementSuppressed(false);

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        if (blackboard != null)
        {
            blackboard.velocity = Vector2.zero;
        }
    }

    public void SetDashVelocity(int direction)
    {
        if (body == null || abilityConfig == null)
        {
            return;
        }

        body.linearVelocity = new Vector2(direction * abilityConfig.dashSpeed, 0f);
    }

    public void BeginLedgeClimb()
    {
        ledgeClimbActive = true;
        EndWallSlide();
        ResetJumpRuntime();
        SetNormalMovementSuppressed(true);
        SetGravitySuspended(true);
        HoldStationary();
    }

    public void SetLedgeClimbPosition(Vector2 position)
    {
        if (ledgeClimbActive)
        {
            SetScriptedPosition(position);
        }
    }

    public void EndLedgeClimb(bool placeAtTarget, Vector2 targetPosition)
    {
        if (placeAtTarget && body != null)
        {
            SetScriptedPosition(targetPosition);
        }

        ledgeClimbActive = false;
        HoldStationary();
        SetGravitySuspended(false);
        SetNormalMovementSuppressed(false);
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

        if (blackboard.binding)
        {
            HoldStationary();
            return;
        }

        if (ledgeClimbActive)
        {
            HoldStationary();
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

        if (scriptedEntryActive)
        {
            if (scriptedLockY)
            {
                body.linearVelocity = scriptedVelocityTarget;
            }
            else
            {
                body.linearVelocity = new Vector2(scriptedVelocityTarget.x, body.linearVelocity.y);
            }
        }

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

    private void SetScriptedPosition(Vector2 position)
    {
        if (body == null)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            return;
        }

        body.linearVelocity = Vector2.zero;
        body.position = position;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        if (blackboard != null)
        {
            blackboard.velocity = Vector2.zero;
        }
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
