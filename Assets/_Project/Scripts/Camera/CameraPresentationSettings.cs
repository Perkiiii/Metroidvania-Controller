using System;
using UnityEngine;

// Authored payload of one camera presentation request. Kept as a serializable struct so Timeline
// clips, scene adapters, and tests all author the same data without a new ScriptableObject type.
// Shared defaults live in CameraConfig; the fields here are per-request intent and overrides.
[Serializable]
public struct CameraPresentationSettings
{
    [Header("Framing")]
    public CameraPresentationMode mode;

    [Tooltip("World position framed by FocusWorldPoint. Ignored by the target-driven modes.")]
    public Vector2 worldPoint;

    [Tooltip("World-space nudge applied to the resolved focus point or multi-target region centre.")]
    public Vector2 framingOffset;

    [Tooltip("Extra world units kept clear on each side of the framed region when auto zoom is on.")]
    public float paddingX;

    [Tooltip("Extra world units kept clear above and below the framed region when auto zoom is on.")]
    public float paddingY;

    [Header("Zoom")]
    [Tooltip("Fit the framed region automatically. When false, authoredZoom is used directly.")]
    public bool autoZoom;

    [Tooltip("Zoom multiplier of the base viewport (1 = the authored CameraConfig framing). Used when autoZoom is off.")]
    public float authoredZoom;

    [Tooltip("Override the shared CameraConfig minimum/maximum zoom for this request.")]
    public bool overrideZoomLimits;
    public float minZoom;
    public float maxZoom;

    [Header("Blend")]
    [Tooltip("Use the blendIn/blendOut settings below instead of the shared CameraConfig presentation defaults.")]
    public bool overrideBlend;
    public CameraTransitionSettings blendIn;
    public CameraTransitionSettings blendOut;

    [Tooltip("0 = fully underlying framing, 1 = fully this request. Timeline clip weight writes this.")]
    [Range(0f, 1f)] public float weight;

    public static CameraPresentationSettings Default(CameraPresentationMode mode)
    {
        return new CameraPresentationSettings
        {
            mode = mode,
            worldPoint = Vector2.zero,
            framingOffset = Vector2.zero,
            paddingX = 0f,
            paddingY = 0f,
            autoZoom = mode == CameraPresentationMode.FrameTargets,
            authoredZoom = 1f,
            overrideZoomLimits = false,
            minZoom = 1f,
            maxZoom = 1f,
            overrideBlend = false,
            blendIn = CameraTransitionSettings.Live(0.2f, 0.4f, false),
            blendOut = CameraTransitionSettings.Live(0.2f, 0.4f, true),
            weight = 1f
        };
    }

    public bool RequiresTargets => mode != CameraPresentationMode.FocusWorldPoint;

    public float ResolvedWeight => float.IsNaN(weight) ? 1f : Mathf.Clamp01(weight);

    public bool IsFinite()
    {
        return IsFinite(worldPoint)
            && IsFinite(framingOffset)
            && float.IsFinite(paddingX)
            && float.IsFinite(paddingY)
            && float.IsFinite(authoredZoom)
            && float.IsFinite(minZoom)
            && float.IsFinite(maxZoom)
            && float.IsFinite(weight)
            && blendIn.IsFinite()
            && blendOut.IsFinite();
    }

    public bool IsNonNegative()
    {
        return paddingX >= 0f
            && paddingY >= 0f
            && authoredZoom > 0f
            && minZoom > 0f
            && maxZoom > 0f
            && blendIn.IsNonNegative()
            && blendOut.IsNonNegative();
    }

    public bool HasValidZoomLimitOrder()
    {
        return !overrideZoomLimits || minZoom <= maxZoom;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y);
    }
}
