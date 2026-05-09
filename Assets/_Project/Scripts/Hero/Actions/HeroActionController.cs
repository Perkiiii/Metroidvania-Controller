using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroActionController : MonoBehaviour
{
    private HeroConfig config;
    private HeroStateBlackboard blackboard;
    private HeroInputReader input;
    private HeroMotor motor;

    private HeroJumpAction jump;
    private HeroDashAction dash;
    private HeroAttackAction attack;
    private HeroWallSlideAction wallSlide;

    public void Initialize(
        HeroConfig heroConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor)
    {
        config = heroConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        motor = heroMotor;

        jump = new HeroJumpAction(config, blackboard, input, heroMotor);
        dash = new HeroDashAction(config, blackboard, input, heroMotor);
        attack = new HeroAttackAction(config, blackboard, input, gameObject, transform);
        wallSlide = new HeroWallSlideAction(config, blackboard, input, heroMotor);
    }

    public void Tick()
    {
        if (config == null || blackboard == null || input == null || motor == null)
        {
            return;
        }

        dash.Tick(Time.deltaTime);
        attack.Tick(Time.deltaTime);
        ApplyLocomotionIntent();
    }

    public void FixedTick(float fixedDeltaTime)
    {
        if (config == null || blackboard == null || input == null || motor == null)
        {
            return;
        }

        wallSlide.FixedTick();
        jump.FixedTick(fixedDeltaTime);
        dash.FixedTick(fixedDeltaTime);
        attack.FixedTick(fixedDeltaTime);
    }

    private void ApplyLocomotionIntent()
    {
        float moveX = blackboard.controlLocked || blackboard.inputBlocked || blackboard.dashing ? 0f : input.MoveVector.x;
        bool wantsRun = input.SprintHeld || !config.requireSprintForRun;
        motor.SetDesiredMove(moveX, wantsRun);

        if (Mathf.Abs(moveX) > config.horizontalInputDeadZone && !blackboard.controlLocked)
        {
            motor.SetFacingDirection(moveX > 0f ? 1 : -1);
        }
    }

    private void OnDrawGizmosSelected()
    {
        attack?.DrawGizmosSelected();
    }
}
