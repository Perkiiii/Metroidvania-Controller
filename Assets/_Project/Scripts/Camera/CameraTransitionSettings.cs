using System;
using UnityEngine;

[Serializable]
public struct CameraTransitionSettings
{
    [Tooltip("Camera damp time (X) used for the duration of this transition.")]
    public float dampTimeX;

    [Tooltip("Camera damp time (Y) used for the duration of this transition.")]
    public float dampTimeY;

    [Tooltip("Seconds spent blending from dampTimeX/Y back to the normal state-driven damp values. 0 applies the state-driven values on the next frame.")]
    public float blendDuration;

    [Tooltip("Zero the SmoothDamp velocity references when this transition begins.")]
    public bool resetVelocity;

    [Tooltip("Apply the destination immediately instead of smoothing (scene start / hidden rebind only).")]
    public bool applyImmediate;

    public static CameraTransitionSettings Live(float dampTime, float blendDuration, bool resetVelocity)
    {
        return new CameraTransitionSettings
        {
            dampTimeX = dampTime,
            dampTimeY = dampTime,
            blendDuration = blendDuration,
            resetVelocity = resetVelocity,
            applyImmediate = false
        };
    }

    public static CameraTransitionSettings Immediate()
    {
        return new CameraTransitionSettings
        {
            dampTimeX = 0f,
            dampTimeY = 0f,
            blendDuration = 0f,
            resetVelocity = true,
            applyImmediate = true
        };
    }

    public bool IsFinite()
    {
        return !float.IsNaN(dampTimeX) && !float.IsInfinity(dampTimeX)
            && !float.IsNaN(dampTimeY) && !float.IsInfinity(dampTimeY)
            && !float.IsNaN(blendDuration) && !float.IsInfinity(blendDuration);
    }

    public bool IsNonNegative()
    {
        return dampTimeX >= 0f && dampTimeY >= 0f && blendDuration >= 0f;
    }
}
