using UnityEngine;

public sealed class HeroWallSlideAction
{
    private readonly HeroConfig config;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly HeroMotor motor;
    private readonly HeroAudioController audio;
    private bool wasWallSliding;

    public HeroWallSlideAction(
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

    public void FixedTick()
    {
        bool shouldSlide = blackboard.wallSliding ? CanContinueWallSlide() : CanEnterWallSlide();

        if (shouldSlide && !wasWallSliding)
        {
            motor.BeginWallSlide();
            audio?.PlayWallSlide();
        }
        else if (!shouldSlide && wasWallSliding)
        {
            motor.EndWallSlide();
            audio?.StopWallSlide();
        }

        blackboard.wallSliding = shouldSlide;
        wasWallSliding = shouldSlide;

        if (shouldSlide)
        {
            motor.StopJumpSustain();
        }
    }

    private bool CanEnterWallSlide()
    {
        return HasWallSlidePhysicalConditions() && IsPressingIntoWall();
    }

    private bool CanContinueWallSlide()
    {
        return HasWallSlidePhysicalConditions() && !IsPressingAwayFromWall();
    }

    private bool HasWallSlidePhysicalConditions()
    {
        return !blackboard.grounded
            && !blackboard.dashing
            && !blackboard.attacking
            && !blackboard.wallJumping
            && !blackboard.recoiling
            && blackboard.touchingWallFront
            && (blackboard.falling || motor.Velocity.y <= 0f);
    }

    private bool IsPressingIntoWall()
    {
        float moveX = input.MoveVector.x;
        return Mathf.Abs(moveX) >= config.wallSlideInputThreshold
            && Mathf.Sign(moveX) == blackboard.FacingDirection;
    }

    private bool IsPressingAwayFromWall()
    {
        float moveX = input.MoveVector.x;
        return Mathf.Abs(moveX) >= config.wallSlideInputThreshold
            && Mathf.Sign(moveX) == -blackboard.FacingDirection;
    }
}
