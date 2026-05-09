using UnityEngine;

public sealed class HeroDashAction
{
    private readonly HeroConfig config;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly HeroMotor motor;

    private float dashTimer;
    private float cooldownTimer;
    private bool airDashUsed;
    private int dashDirection = 1;

    public HeroDashAction(
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

    public void Tick(float deltaTime)
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= deltaTime;
        }

        if (blackboard.grounded)
        {
            airDashUsed = false;
        }

        if (input.DashPressedThisFrame && CanStartDash())
        {
            StartDash();
        }
    }

    public void FixedTick(float fixedDeltaTime)
    {
        if (!blackboard.dashing)
        {
            return;
        }

        motor.SetDashVelocity(dashDirection);
        dashTimer -= fixedDeltaTime;

        if (dashTimer <= 0f)
        {
            StopDash();
        }
    }

    private bool CanStartDash()
    {
        if (blackboard.controlLocked || blackboard.inputBlocked || blackboard.dashing || blackboard.attacking || blackboard.attackRecovering)
        {
            return false;
        }

        if (cooldownTimer > 0f)
        {
            return false;
        }

        return blackboard.grounded || !airDashUsed || blackboard.wallSliding;
    }

    private void StartDash()
    {
        float moveX = input.MoveVector.x;
        if (Mathf.Abs(moveX) > config.horizontalInputDeadZone)
        {
            dashDirection = moveX > 0f ? 1 : -1;
            motor.SetFacingDirection(dashDirection);
        }
        else
        {
            dashDirection = blackboard.FacingDirection;
        }

        if (!blackboard.grounded)
        {
            airDashUsed = true;
        }

        blackboard.dashing = true;
        blackboard.wallSliding = false;
        blackboard.jumpSustaining = false;
        blackboard.jumping = false;
        dashTimer = config.dashDuration;
        cooldownTimer = config.dashCooldown;

        motor.StopJumpSustain();
        motor.SetNormalMovementSuppressed(true);
        motor.SetGravitySuspended(true);
        motor.SetDashVelocity(dashDirection);
    }

    private void StopDash()
    {
        blackboard.dashing = false;
        motor.SetGravitySuspended(false);
        motor.SetNormalMovementSuppressed(false);
    }
}
