using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroCameraSignalBridge : MonoBehaviour
{
    private HeroStateBlackboard blackboard;
    private HeroInputReader input;
    private float manualLookHoldTimer;
    private float manualLookDirection;

    public void Initialize(HeroStateBlackboard heroBlackboard, HeroInputReader inputReader)
    {
        blackboard = heroBlackboard;
        input = inputReader;
        ResetManualLook();
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

        float lookInput = GetManualLookInput(controller);
        controller.SetLookInput(lookInput);

        target.SetFacingDirection(blackboard.FacingDirection);
        target.SetVelocityHint(blackboard.velocity);
        target.SetDashActive(blackboard.dashing, blackboard.FacingDirection);
        target.SetSprintActive(
            blackboard.sprinting || blackboard.sprintJumpCarrying,
            blackboard.sprintDirection != 0 ? blackboard.sprintDirection : blackboard.FacingDirection);
    }

    private float GetManualLookInput(CameraController controller)
    {
        if (ShouldCancelManualLook())
        {
            ResetManualLook();
            return 0f;
        }

        float rawLookInput = input.MoveVector.y;
        float threshold = controller != null ? controller.LookInputThreshold : 0.5f;
        if (Mathf.Abs(rawLookInput) < threshold)
        {
            ResetManualLook();
            return 0f;
        }

        float lookDirection = Mathf.Sign(rawLookInput);
        if (!Mathf.Approximately(lookDirection, manualLookDirection))
        {
            manualLookHoldTimer = 0f;
            manualLookDirection = lookDirection;
        }

        manualLookHoldTimer += Time.deltaTime;
        float holdDelay = controller != null ? controller.ManualLookHoldDelay : 2f;
        return manualLookHoldTimer >= holdDelay ? manualLookDirection : 0f;
    }

    private bool ShouldCancelManualLook()
    {
        return blackboard.controlLocked
            || blackboard.inputBlocked
            || !blackboard.grounded
            || blackboard.moving
            || blackboard.binding
            || blackboard.dashing
            || blackboard.attacking;
    }

    private void ResetManualLook()
    {
        manualLookHoldTimer = 0f;
        manualLookDirection = 0f;
    }
}
