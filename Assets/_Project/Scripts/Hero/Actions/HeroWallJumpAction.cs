public sealed class HeroWallJumpAction
{
    private readonly HeroConfig config;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly HeroMotor motor;
    private readonly HeroAudioController audio;

    private float relatchLockoutTimer;

    public HeroWallJumpAction(
        HeroConfig heroConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor,
        HeroAudioController heroAudio)
    {
        config = heroConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        motor = heroMotor;
        audio = heroAudio;
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

        audio?.PlayWallJump();
        motor.StartWallJump(wallDirection);
    }

    private void ClearWallJumping()
    {
        blackboard.wallJumping = false;
        relatchLockoutTimer = 0f;
    }
}
