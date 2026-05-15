using UnityEngine;

[CreateAssetMenu(menuName = "World/Camera Config", fileName = "CameraConfig")]
public sealed class CameraConfig : ScriptableObject
{
    [Header("Target Smoothing")]
    public float targetDampTimeNormal = 0.35f;
    public float targetDampTimeSlow = 0.15f;
    public float targetDampTimeSlower = 0.45f;
    public float slowTime = 0.5f;
    public float shortSlowTime = 0.25f;
    public float snapDistance = 0.15f;

    [Header("Look-ahead")]
    public float xLookAhead = 0.16f;
    public float dashLookAhead = 2.51f;
    public float fallLookAhead = 1.25f;
    public float sprintLookAhead = 1.25f;
    public float specialMoveLookAhead = 2.5f;
    public float lookAheadMoveSpeed = 4f;

    [Header("Motion Inference")]
    public float facingVelocityThreshold = 0.05f;
    public float dashVelocityThreshold = 5f;
    public float fallingVelocityThreshold = 0.25f;
    public float risingVelocityThreshold = 0.25f;
    public float fastFallVelocityThreshold = -8f;

    [Header("Vertical Framing")]
    public float baseVerticalOffset = 1f;
    public float fastFallVerticalOffset = -1.5f;
    public float verticalOffsetMoveSpeed = 5f;
    public float fallCatchAccel = 80f;
    public float fallCatchMax = 25f;

    [Header("Offset Areas")]
    public float maxCombinedOffsetAreaMagnitude = 6f;

    [Header("Camera Smoothing")]
    public float cameraDampTimeNormal = 0.32f;
    public float cameraDampTimeSlow = 0.15f;
    public float cameraDampTimeYGrounded = 0.32f;
    public float cameraDampTimeYRising = 0.38f;
    public float cameraDampTimeYFalling = 0.24f;
    public float cameraDampTimeYMin = 0.03f;
    public float maxVelocity = 65f;

    [Header("Look Input")]
    public float lookInputOffset = 6f;
    public float lookInputThreshold = 0.5f;
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
}
