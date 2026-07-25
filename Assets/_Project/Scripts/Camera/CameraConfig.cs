using UnityEngine;

[CreateAssetMenu(menuName = "World/Camera Config", fileName = "CameraConfig")]
public sealed class CameraConfig : ScriptableObject
{
    [Header("Target Smoothing")]
    public float targetDampTimeNormal = 0.22f;
    public float targetDampTimeSlow = 0.12f;
    public float targetDampTimeSlower = 0.35f;
    public float slowTime = 0.5f;
    public float shortSlowTime = 0.25f;
    public float snapDistance = 0.10f;

    [Header("Look-ahead")]
    public float xLookAhead = 0.35f;
    public float dashLookAhead = 2.15f;
    public float fallLookAhead = 0.40f;
    public float sprintLookAhead = 0.75f;
    public float specialMoveLookAhead = 1.75f;
    public float lookAheadMoveSpeed = 6f;
    public float lookAheadIdleMoveSpeed = 1.2f;
    [Range(0f, 1f)] public float stationaryLookAheadMultiplier = 0.2f;

    [Header("Motion Inference")]
    public float facingVelocityThreshold = 0.05f;
    public float dashVelocityThreshold = 5f;
    public float fallingVelocityThreshold = 0.25f;
    public float risingVelocityThreshold = 0.25f;
    public float fastFallVelocityThreshold = -8f;

    [Header("Vertical Framing")]
    public float baseVerticalOffset = 0.85f;
    public float fastFallVerticalOffset = -0.75f;
    public float verticalOffsetMoveSpeed = 7f;
    public float fallCatchAccel = 90f;
    public float fallCatchMax = 24f;

    [Header("Offset Areas")]
    public float maxCombinedOffsetAreaMagnitude = 6f;

    [Header("Camera Smoothing")]
    public float cameraDampTimeNormal = 0.18f;
    public float cameraDampTimeSlow = 0.15f;
    public float cameraDampTimeYGrounded = 0.18f;
    public float cameraDampTimeYRising = 0.12f;
    public float cameraDampTimeYFalling = 0.10f;
    public float cameraDampTimeYFastFalling = 0.07f;
    public float cameraDampTimeYMin = 0.03f;
    public float maxVelocity = 90f;

    [Header("Look Input")]
    public float lookInputOffset = 4.0f;
    public float lookInputThreshold = 0.5f;
    public float manualLookHoldDelay = 0.5f;
    public float lookInputSlowTime = 0.35f;
    public float lookInputDampTimeY = 0.35f;
    public float lookInputMoveSpeed = 7f;

    [Header("Projection")]
    public float fieldOfView = 24f;
    public float cameraZ = -38.1f;
    public float nearClipPlane = 0.3f;
    public float farClipPlane = 1000f;

    [Header("Locking")]
    public float lockLargeJumpDistance = 9f;

    [Header("Scene Start")]
    public float startLockedTimer = 0.65f;
    public float positioningSettleTime = 0.1f;

    [Header("Lock Transitions")]
    [Tooltip("Applied immediately: scene start, hidden rebind, and other synchronous placement.")]
    public CameraTransitionSettings sceneStartTransition = CameraTransitionSettings.Immediate();
    [Tooltip("Live follow -> first lock entry.")]
    public CameraTransitionSettings followToLockTransition = CameraTransitionSettings.Live(0.15f, 0.35f, false);
    [Tooltip("Live switch between two overlapping locks.")]
    public CameraTransitionSettings lockToLockTransition = CameraTransitionSettings.Live(0.15f, 0.35f, false);
    [Tooltip("Live exit from the final active lock back to room follow.")]
    public CameraTransitionSettings lockToFollowTransition = CameraTransitionSettings.Live(0.15f, 0.35f, false);
    [Tooltip("The final freeze/free override releases back to the current underlying framing.")]
    public CameraTransitionSettings overrideReleasedTransition = CameraTransitionSettings.Live(0.15f, 0.35f, true);
}
