public sealed class HeroJumpAction
{
    private readonly HeroConfig config;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly HeroMotor motor;

    private float coyoteTimer;

    public HeroJumpAction(
        HeroConfig heroConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor)
    {
        config = heroConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        motor = heroMotor;
    }

    public void FixedTick(float fixedDeltaTime)
    {
        UpdateCoyoteTimer(fixedDeltaTime);

        if (blackboard.grounded && !blackboard.wasGrounded && motor.Velocity.y <= 0.01f)
        {
            motor.ResetJumpRuntime();
        }

        bool startedJumpThisTick = false;
        if (CanStartJump())
        {
            input.ConsumeJumpBuffer();
            coyoteTimer = 0f;
            motor.StartJump();
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

    private bool CanCutCurrentJump()
    {
        return blackboard.jumpedSteps > 0
            && !blackboard.grounded
            && motor.Velocity.y > 0f;
    }
}
