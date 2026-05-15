using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroCameraSignalBridge : MonoBehaviour
{
    private HeroStateBlackboard blackboard;
    private HeroInputReader input;

    public void Initialize(HeroStateBlackboard heroBlackboard, HeroInputReader inputReader)
    {
        blackboard = heroBlackboard;
        input = inputReader;
    }

    public void Tick()
    {
        if (input == null || blackboard == null)
        {
            return;
        }

        CameraController controller = GameCameras.Instance != null ? GameCameras.Instance.Controller : null;
        CameraTarget target = GameCameras.Instance != null ? GameCameras.Instance.Target : null;

        if (controller == null || target == null)
        {
            return;
        }

        float lookInput = !blackboard.controlLocked && !blackboard.inputBlocked && blackboard.grounded && !blackboard.moving
            ? input.MoveVector.y
            : 0f;
        controller.SetLookInput(lookInput);

        target.SetFacingDirection(blackboard.FacingDirection);
        target.SetVelocityHint(blackboard.velocity);
        target.SetDashActive(blackboard.dashing, blackboard.FacingDirection);
        target.SetSprintActive(input.SprintHeld && blackboard.grounded, blackboard.FacingDirection);
    }
}
