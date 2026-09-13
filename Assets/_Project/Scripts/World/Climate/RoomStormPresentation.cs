using UnityEngine;

/// <summary>
/// Small, scene-local state machine for storm strikes. The state is deliberately transient:
/// weather simulation owns the request and this component only owns the current presentation.
/// </summary>
public enum RoomStormPresentationState
{
    Inactive,
    WaitingForStrike,
    Flashing,
    WaitingForThunder
}

/// <summary>
/// Presents a restrained lightning flash and delayed thunder while the room requests Storm.
/// Rain remains a separate consumer of the same request, so this component never owns particles,
/// weather state, lighting, or a scheduler shared with other systems.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomStormPresentation : MonoBehaviour
{
    [Header("Request and authored effects")]
    [SerializeField] private RoomWeatherPresentation weatherPresentation;
    [SerializeField] private SpriteRenderer flashRenderer;
    [SerializeField] private AudioClip thunderClip;

    [Header("Strike cadence")]
    [SerializeField, Min(0f)] private float initialStrikeDelay = 2.5f;
    [SerializeField, Min(0.05f)] private float strikeIntervalMinSeconds = 8f;
    [SerializeField, Min(0.05f)] private float strikeIntervalMaxSeconds = 14f;

    [Header("Flash envelope")]
    [SerializeField, Range(1, 4)] private int flashPulseCount = 3;
    [SerializeField, Min(0f)] private float flashPulseDuration = 0.055f;
    [SerializeField, Min(0f)] private float flashPulseGap = 0.04f;
    [SerializeField, Range(0f, 1f)] private float flashPeakAlpha = 0.72f;
    [SerializeField] private Color flashColor = new Color(0.84f, 0.92f, 1f, 1f);
    [SerializeField, Min(0f)] private float thunderDelaySeconds = 0.32f;

    [Header("Thunder audio")]
    [SerializeField, Range(0f, 1f)] private float thunderVolume = 0.55f;
    [SerializeField, Min(0.01f)] private float thunderPitchMin = 0.96f;
    [SerializeField, Min(0.01f)] private float thunderPitchMax = 1.04f;

    [Header("Camera coverage")]
    [SerializeField, Min(0f)] private float cameraPadding = 0.5f;
    [SerializeField, Min(0.01f)] private float depthFromCamera = 37.8f;

    private RoomWeatherPresentation subscribedPresentation;
    private RoomStormPresentationState currentState = RoomStormPresentationState.Inactive;
    private RoomWeatherPresentationMode currentRequest = RoomWeatherPresentationMode.Clear;
    private float stateTimer;
    private int pulseIndex;
    private bool pulseOn;
    private bool stormRequested;
    private float currentFlashAmount;
    private int strikeCount;
    private int thunderPlayCount;

    /// <summary>Latest presentation request observed from the room weather seam.</summary>
    public RoomWeatherPresentationMode CurrentRequest => currentRequest;

    /// <summary>Readable transient state for diagnostics and focused tests.</summary>
    public RoomStormPresentationState CurrentState => currentState;

    /// <summary>True while the request is Storm, including flash/thunder phases.</summary>
    public bool IsStormRequested => stormRequested;

    /// <summary>True only while one delayed thunder one-shot is pending.</summary>
    public bool IsThunderPending => currentState == RoomStormPresentationState.WaitingForThunder;

    /// <summary>Current normalized flash amount (zero when hidden).</summary>
    public float FlashAmount => currentFlashAmount;

    /// <summary>Number of completed strike envelopes since this component was enabled.</summary>
    public int StrikeCount => strikeCount;

    /// <summary>Number of thunder requests sent through AudioManager.</summary>
    public int ThunderPlayCount => thunderPlayCount;

    /// <summary>Authored flash renderer, exposed read-only for production asset validation.</summary>
    public SpriteRenderer FlashRenderer => flashRenderer;

    /// <summary>Remaining cadence time while waiting for the next strike.</summary>
    public float TimeUntilStrike => currentState == RoomStormPresentationState.WaitingForStrike
        ? stateTimer
        : 0f;

    /// <summary>Remaining intentional delay before thunder fires.</summary>
    public float TimeUntilThunder => currentState == RoomStormPresentationState.WaitingForThunder
        ? stateTimer
        : 0f;

    private void Awake()
    {
        HideFlash();
    }

    private void OnEnable()
    {
        HideFlash();
        Subscribe();

        if (weatherPresentation != null)
        {
            ApplyPresentation(weatherPresentation.CurrentPresentation);
        }
        else
        {
            ApplyPresentation(RoomWeatherPresentationMode.Clear);
        }
    }

    private void Update()
    {
        TickStorm(Time.unscaledDeltaTime);
    }

    private void LateUpdate()
    {
        UpdateCameraCoverage();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ApplyPresentation(RoomWeatherPresentationMode.Clear);
    }

    private void OnDestroy()
    {
        // Keep teardown idempotent: scene unload can reach both callbacks, and no delayed
        // coroutine/timer callback should survive the presentation object's lifetime.
        Unsubscribe();
        ApplyPresentation(RoomWeatherPresentationMode.Clear);
    }

    private void Subscribe()
    {
        if (weatherPresentation == null || subscribedPresentation == weatherPresentation)
        {
            return;
        }

        Unsubscribe();
        subscribedPresentation = weatherPresentation;
        subscribedPresentation.PresentationChanged += HandlePresentationChanged;
    }

    private void Unsubscribe()
    {
        if (subscribedPresentation == null)
        {
            subscribedPresentation = null;
            return;
        }

        subscribedPresentation.PresentationChanged -= HandlePresentationChanged;
        subscribedPresentation = null;
    }

    private void HandlePresentationChanged(RoomWeatherPresentationMode presentation)
    {
        ApplyPresentation(presentation);
    }

    private void ApplyPresentation(RoomWeatherPresentationMode presentation)
    {
        currentRequest = presentation;
        if (presentation == RoomWeatherPresentationMode.Storm)
        {
            ActivateStorm();
        }
        else
        {
            CancelStorm();
        }
    }

    private void ActivateStorm()
    {
        if (stormRequested)
        {
            return;
        }

        stormRequested = true;
        currentState = RoomStormPresentationState.WaitingForStrike;
        stateTimer = Mathf.Max(0f, initialStrikeDelay);
        pulseIndex = 0;
        pulseOn = false;
        HideFlash();
    }

    private void CancelStorm()
    {
        stormRequested = false;
        currentState = RoomStormPresentationState.Inactive;
        stateTimer = 0f;
        pulseIndex = 0;
        pulseOn = false;
        HideFlash();
    }

    /// <summary>
    /// Advances the local state machine using unscaled seconds. A strike is intentionally started
    /// on one update and advanced on later updates, keeping pulse and thunder phases inspectable
    /// and preventing a zero-duration tuning mistake from spinning through multiple strikes.
    /// </summary>
    private void TickStorm(float deltaTime)
    {
        if (!stormRequested)
        {
            return;
        }

        float remaining = Mathf.Max(0f, deltaTime);
        int transitions = 0;
        while (transitions++ < 32)
        {
            switch (currentState)
            {
                case RoomStormPresentationState.WaitingForStrike:
                    if (stateTimer > 0f)
                    {
                        if (remaining < stateTimer)
                        {
                            stateTimer -= remaining;
                            return;
                        }

                        remaining -= stateTimer;
                        stateTimer = 0f;
                    }

                    BeginStrike();
                    return;

                case RoomStormPresentationState.Flashing:
                    if (stateTimer > 0f)
                    {
                        if (remaining < stateTimer)
                        {
                            stateTimer -= remaining;
                            if (pulseOn)
                            {
                                SetFlashAmount(EvaluatePulseAmount());
                            }

                            return;
                        }

                        remaining -= stateTimer;
                        stateTimer = 0f;
                    }

                    CompleteFlashPhase();
                    if (currentState == RoomStormPresentationState.WaitingForThunder)
                    {
                        // Thunder has its own deliberate delay; do not consume it in the same
                        // update that ends the visible flash envelope.
                        return;
                    }

                    if (remaining <= 0f)
                    {
                        return;
                    }

                    continue;

                case RoomStormPresentationState.WaitingForThunder:
                    if (stateTimer > 0f)
                    {
                        if (remaining < stateTimer)
                        {
                            stateTimer -= remaining;
                            return;
                        }

                        remaining -= stateTimer;
                        stateTimer = 0f;
                    }

                    FireThunder();
                    // The next cadence starts on a later update. This leaves at most one pending
                    // strike/thunder at a time while allowing an already-fired pooled one-shot to
                    // finish naturally.
                    return;

                default:
                    return;
            }
        }
    }

    private void BeginStrike()
    {
        currentState = RoomStormPresentationState.Flashing;
        pulseIndex = 0;
        pulseOn = true;
        stateTimer = Mathf.Max(0.001f, flashPulseDuration);
        SetFlashAmount(Mathf.Clamp01(flashPeakAlpha));
    }

    private void CompleteFlashPhase()
    {
        SetFlashAmount(0f);

        if (pulseOn)
        {
            pulseOn = false;
            pulseIndex++;
            strikeCount = pulseIndex >= Mathf.Max(1, flashPulseCount)
                ? strikeCount + 1
                : strikeCount;

            if (pulseIndex >= Mathf.Max(1, flashPulseCount))
            {
                currentState = RoomStormPresentationState.WaitingForThunder;
                stateTimer = Mathf.Max(0f, thunderDelaySeconds);
                return;
            }

            stateTimer = Mathf.Max(0.001f, flashPulseGap);
            return;
        }

        // Gap finished; begin the next restrained pulse without starting a new strike.
        pulseOn = true;
        stateTimer = Mathf.Max(0.001f, flashPulseDuration);
        SetFlashAmount(Mathf.Clamp01(flashPeakAlpha));
    }

    private void FireThunder()
    {
        if (!stormRequested)
        {
            return;
        }

        if (thunderClip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(
                thunderClip,
                Mathf.Min(thunderPitchMin, thunderPitchMax),
                Mathf.Max(thunderPitchMin, thunderPitchMax),
                Mathf.Clamp01(thunderVolume));
            thunderPlayCount++;
        }

        currentState = RoomStormPresentationState.WaitingForStrike;
        stateTimer = SampleCadenceSeconds();
        pulseIndex = 0;
        pulseOn = false;
        HideFlash();
    }

    private float SampleCadenceSeconds()
    {
        float minimum = Mathf.Max(0.05f, strikeIntervalMinSeconds);
        float maximum = Mathf.Max(minimum, strikeIntervalMaxSeconds);
        return Random.Range(minimum, maximum);
    }

    private float EvaluatePulseAmount()
    {
        float duration = Mathf.Max(0.001f, flashPulseDuration);
        float progress = 1f - Mathf.Clamp01(stateTimer / duration);
        // Hold the peak briefly, then taper each pulse so the sequence reads as a flash rather
        // than a sustained overlay.
        float fade = progress <= 0.35f
            ? 1f
            : 1f - Mathf.Clamp01((progress - 0.35f) / 0.65f);
        return Mathf.Clamp01(flashPeakAlpha) * fade;
    }

    private void SetFlashAmount(float amount)
    {
        currentFlashAmount = Mathf.Clamp01(amount);
        if (flashRenderer == null)
        {
            return;
        }

        Color color = flashColor;
        color.a = Mathf.Clamp01(flashColor.a) * currentFlashAmount;
        flashRenderer.color = color;
        flashRenderer.enabled = currentFlashAmount > 0.0001f;
    }

    private void HideFlash()
    {
        SetFlashAmount(0f);
    }

    private void UpdateCameraCoverage()
    {
        if (flashRenderer == null)
        {
            return;
        }

        Camera camera = CameraInfoCache.MainCamera;
        if (camera == null)
        {
            camera = Camera.main;
        }

        if (camera == null)
        {
            return;
        }

        if (CameraInfoCache.MainCamera != camera || CameraInfoCache.FrameStamp != Time.frameCount)
        {
            CameraInfoCache.UpdateCache(camera);
        }

        Rect worldRect = CameraInfoCache.WorldRect;
        if (worldRect.width <= 0f || worldRect.height <= 0f)
        {
            return;
        }

        Transform flashTransform = flashRenderer.transform;
        Vector3 position = flashTransform.position;
        position.x = worldRect.center.x;
        position.y = worldRect.center.y;
        position.z = camera.transform.position.z + Mathf.Max(0.01f, depthFromCamera);
        flashTransform.position = position;

        Vector2 spriteSize = flashRenderer.sprite != null
            ? flashRenderer.sprite.bounds.size
            : Vector2.one;
        float width = Mathf.Max(0.01f, spriteSize.x);
        float height = Mathf.Max(0.01f, spriteSize.y);
        Vector3 parentScale = flashTransform.parent != null
            ? flashTransform.parent.lossyScale
            : Vector3.one;
        float parentWidth = Mathf.Max(0.01f, Mathf.Abs(parentScale.x));
        float parentHeight = Mathf.Max(0.01f, Mathf.Abs(parentScale.y));

        flashTransform.localScale = new Vector3(
            (worldRect.width + cameraPadding * 2f) / width / parentWidth,
            (worldRect.height + cameraPadding * 2f) / height / parentHeight,
            1f);
    }
}
