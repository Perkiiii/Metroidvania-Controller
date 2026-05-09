using UnityEngine;

public sealed class HeroWallSlideAction
{
    private readonly HeroConfig config;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly HeroMotor motor;

    public HeroWallSlideAction(
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

    public void FixedTick()
    {
        bool shouldSlide = CanWallSlide();
        blackboard.wallSliding = shouldSlide;

        if (shouldSlide)
        {
            motor.StopJumpSustain();
        }
    }

    private bool CanWallSlide()
    {
        if (blackboard.grounded || blackboard.dashing || blackboard.attacking || blackboard.recoiling)
        {
            return false;
        }

        if (!blackboard.touchingWallFront)
        {
            return false;
        }

        if (!(blackboard.falling || motor.Velocity.y <= 0f))
        {
            return false;
        }

        float moveX = input.MoveVector.x;
        return Mathf.Abs(moveX) >= config.wallSlideInputThreshold
            && Mathf.Sign(moveX) == blackboard.FacingDirection;
    }
}
