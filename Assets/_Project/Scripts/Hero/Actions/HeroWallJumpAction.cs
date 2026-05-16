public sealed class HeroWallJumpAction
{
    private readonly HeroConfig config;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly HeroMotor motor;

    private float relatchLockoutTimer;

    public HeroWallJumpAction(
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
        if (blackboard.grounded)
        {
            ClearWallJumping();
            return;
        }

        if (blackboard.wallJumping)
        {
            relatchLockoutTimer -= fixedDeltaTime;
            if (relatchLockoutTimer <= 0f)
            {
                ClearWallJumping();
            }
        }

        if (CanStartWallJump())
        {
            StartWallJump();
        }
    }

    private bool CanStartWallJump()
    {
        return blackboard.wallSliding
            && !blackboard.controlLocked
            && !blackboard.inputBlocked
            && !blackboard.dashing
            && !blackboard.attackRecovering
            && input.HasBufferedJump;
    }

    private void StartWallJump()
    {
        int wallDirection = blackboard.FacingDirection;

        input.ConsumeJumpBuffer();
        blackboard.wallSliding = false;
        blackboard.wallJumping = true;
        relatchLockoutTimer = config.wallJumpRelatchLockout;

        motor.StartWallJump(wallDirection);
    }

    private void ClearWallJumping()
    {
        blackboard.wallJumping = false;
        relatchLockoutTimer = 0f;
    }
}
