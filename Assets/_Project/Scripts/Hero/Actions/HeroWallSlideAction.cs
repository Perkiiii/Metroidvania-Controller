using UnityEngine;

public sealed class HeroWallSlideAction
{
    private readonly HeroConfig config;
    private readonly HeroAbilityConfig abilityConfig;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly HeroMotor motor;
    private readonly HeroAudioController audio;
    private readonly PlayerAbilityState abilityState;
    private bool wasWallSliding;

    public HeroWallSlideAction(
        HeroConfig heroConfig,
        HeroAbilityConfig heroAbilityConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor,
        HeroAudioController heroAudio,
        PlayerAbilityState abilityState)
    {
        config = heroConfig;
        abilityConfig = heroAbilityConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        motor = heroMotor;
        audio = heroAudio;
        this.abilityState = abilityState;
    }

    public void FixedTick(bool suppressNewEntry = false)
    {
        FixedTick(suppressNewEntry, input != null ? input.MoveVector.x : 0f);
    }

    public void FixedTick(bool suppressNewEntry, float effectiveMoveX)
    {
        bool shouldSlide = blackboard.wallSliding
            ? CanContinueWallSlide(effectiveMoveX)
            : !suppressNewEntry && CanEnterWallSlide(effectiveMoveX);

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

    private bool CanEnterWallSlide(float moveX)
    {
        if (abilityState == null || !abilityState.wallClingUnlocked)
            return false;

        if (abilityConfig == null)
            return false;

        return HasWallSlidePhysicalConditions() && IsPressingIntoWall(moveX);
    }

    private bool CanContinueWallSlide(float moveX)
    {
        return HasWallSlidePhysicalConditions() && !IsPressingAwayFromWall(moveX);
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

    private bool IsPressingIntoWall(float moveX)
    {
        return Mathf.Abs(moveX) >= abilityConfig.wallSlideInputThreshold
            && Mathf.Sign(moveX) == blackboard.FacingDirection;
    }

    private bool IsPressingAwayFromWall(float moveX)
    {
        return Mathf.Abs(moveX) >= abilityConfig.wallSlideInputThreshold
            && Mathf.Sign(moveX) == -blackboard.FacingDirection;
    }
}
