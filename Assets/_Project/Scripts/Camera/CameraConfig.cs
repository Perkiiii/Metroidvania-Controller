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

    [Header("Presentation Transitions (Camera Phase 3)")]
    [Tooltip("A presentation request becomes the selected one while none was active.")]
    public CameraTransitionSettings presentationEnterTransition = CameraTransitionSettings.Live(0.22f, 0.45f, false);
    [Tooltip("The selected presentation request is replaced by a different one.")]
    public CameraTransitionSettings presentationChangeTransition = CameraTransitionSettings.Live(0.22f, 0.45f, false);
    [Tooltip("The final presentation request ends and the camera resolves the current underlying framing.")]
    public CameraTransitionSettings presentationReleaseTransition = CameraTransitionSettings.Live(0.2f, 0.45f, true);

    [Header("Presentation Framing (Camera Phase 3)")]
    [Tooltip("Default world units kept clear left/right of an automatically framed region.")]
    public float presentationPaddingX = 3f;
    [Tooltip("Default world units kept clear above/below an automatically framed region.")]
    public float presentationPaddingY = 2f;

    [Header("Presentation Zoom (Camera Phase 3)")]
    [Tooltip("Zoom is a multiplier of the base viewport implied by fieldOfView/cameraZ. 1 = authored framing. "
        + "Zoom is applied as field of view; the camera is never dollied along Z.")]
    public float minZoom = 0.85f;
    [Tooltip("Largest permitted zoom-out multiplier. Must be >= minZoom.")]
    public float maxZoom = 1.6f;
    [Tooltip("Damp time used while zooming out (expanding). Kept short so targets stay visible.")]
    public float zoomOutDampTime = 0.25f;
    [Tooltip("Damp time used while zooming in (contracting). Kept longer to avoid oscillation.")]
    public float zoomInDampTime = 0.55f;
    [Tooltip("Maximum zoom-multiplier change per second. 0 disables the clamp.")]
    public float maxZoomSpeed = 1.2f;
    [Tooltip("Desired-zoom changes smaller than this are ignored, preventing pumping when targets jitter.")]
    public float zoomHysteresis = 0.02f;
    [Tooltip("Extra dead-band applied only when contracting, so shrinking regions do not chase every frame.")]
    public float zoomContractHysteresis = 0.06f;
    [Tooltip("Damp time used for presentation centre movement, blended with the normal transition damp times.")]
    public float presentationCentreDampTime = 0.25f;
}
