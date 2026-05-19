public sealed class HeroJumpAction
{
    private readonly HeroConfig config;
    private readonly HeroAbilityConfig abilityConfig;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly HeroMotor motor;
    private readonly HeroAudioController audio;
    private readonly PlayerAbilityState abilityState;

    private float coyoteTimer;
    private bool doubleJumpConsumed;
    private bool wasWallSliding;

    public HeroJumpAction(
        HeroConfig heroConfig,
        HeroAbilityConfig heroAbilityConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor,
        HeroAudioController heroAudio,
        PlayerAbilityState heroAbilityState)
    {
        config = heroConfig;
        abilityConfig = heroAbilityConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        motor = heroMotor;
        audio = heroAudio;
        abilityState = heroAbilityState;
    }

    public void FixedTick(float fixedDeltaTime)
    {
        UpdateCoyoteTimer(fixedDeltaTime);

        if (blackboard.grounded && !blackboard.wasGrounded && motor.Velocity.y <= 0.01f)
        {
            motor.ResetJumpRuntime();
            doubleJumpConsumed = false;
        }

        // Reset double jump when entering wall slide (if configured).
        if (abilityConfig != null && abilityConfig.resetDoubleJumpOnWallSlide)
        {
            if (blackboard.wallSliding && !wasWallSliding)
            {
                doubleJumpConsumed = false;
            }
        }
        wasWallSliding = blackboard.wallSliding;

        bool startedJumpThisTick = false;
        if (CanStartJump())
        {
            input.ConsumeJumpBuffer();
            coyoteTimer = 0f;
            motor.StartJump();
            audio?.PlayJump();
            startedJumpThisTick = true;
        }

        if (!startedJumpThisTick && CanDoubleJump())
        {
            input.ConsumeJumpBuffer();
            doubleJumpConsumed = true;
            motor.StartDoubleJump(abilityConfig.doubleJumpSpeed);
            audio?.PlayJump();
            startedJumpThisTick = true;
        }

        if (blackboard.jumpSustaining)
        {
            if (input.JumpReleasedThisFrame || !input.JumpHeld)
            {
                if (!startedJumpThisTick)
                {
                    motor.RequestJumpRelease();
                    input.ConsumeJumpRelease();
                }
            }

            if (!startedJumpThisTick && blackboard.jumpSustaining)
            {
                motor.SustainJump();
            }
        }
        else if (input.JumpReleasedThisFrame)
        {
            if (CanCutCurrentJump())
            {
                motor.RequestJumpRelease();
            }

            input.ConsumeJumpRelease();
        }
    }

    private void UpdateCoyoteTimer(float fixedDeltaTime)
    {
        if (blackboard.grounded && motor.Velocity.y <= 0.01f)
        {
            coyoteTimer = config.coyoteTime;
            return;
        }

        if (coyoteTimer > 0f)
        {
            coyoteTimer -= fixedDeltaTime;
        }
    }

    private bool CanStartJump()
    {
        if (blackboard.controlLocked || blackboard.inputBlocked || blackboard.dashing || !input.HasBufferedJump)
        {
            return false;
        }

        if (blackboard.jumpSustaining)
        {
            return false;
        }

        return (blackboard.grounded && motor.Velocity.y <= 0.01f) || coyoteTimer > 0f;
    }

    private bool CanDoubleJump()
    {
        if (abilityState == null || !abilityState.doubleJumpUnlocked)
            return false;

        if (abilityConfig == null)
            return false;

        if (doubleJumpConsumed)
            return false;

        if (blackboard.controlLocked || blackboard.inputBlocked)
            return false;

        if (blackboard.dashing || blackboard.recoiling)
            return false;

        // Coyote jump takes priority — if coyote is still available CanStartJump fires first.
        // Guard here as a safety net in case CanStartJump was blocked for another reason.
        if (blackboard.grounded || coyoteTimer > 0f)
            return false;

        if (blackboard.wallSliding || blackboard.wallJumping)
            return false;

        if (blackboard.jumpSustaining)
            return false;

        return input.HasBufferedJump;
    }

    private bool CanCutCurrentJump()
    {
        return blackboard.jumpedSteps > 0
            && !blackboard.grounded
            && motor.Velocity.y > 0f;
    }
}
