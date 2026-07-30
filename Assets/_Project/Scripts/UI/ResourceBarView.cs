using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum ResourceBarChangePresentation
{
    Snap,
    Gain,
    Spend,
    Cleared
}

/// <summary>
/// Presentation-only resource bar. Production uses a full-width masked fill; the legacy
/// <see cref="Image.fillAmount"/> reference remains only as a safe migration/test fallback.
/// </summary>
[DisallowMultipleComponent]
public sealed class ResourceBarView : MonoBehaviour
{
    [SerializeField] private HorizontalMaskedFillView maskedFill;
    [SerializeField] private Image fillImage;
    [SerializeField] private CanvasGroup edgePulse;
    [SerializeField] private RectTransform animatedRoot;
    [SerializeField, Min(0f)] private float gainDuration = 0.12f;
    [SerializeField, Min(0f)] private float spendDuration = 0.09f;
    [SerializeField, Min(0f)] private float pulseDuration = 0.14f;

    public float FillAmount01 { get; private set; }
    public bool UsesMaskedFill => maskedFill != null;

    private Coroutine fillRoutine;
    private Coroutine pulseRoutine;

    private void OnDisable()
    {
        StopPresentation();
        ApplyFill(FillAmount01);
        ApplyPulse(0f, 1f);
    }

    public void SetFill(float value)
    {
        SetFill(value, ResourceBarChangePresentation.Snap);
    }

    public void SetFill(float value, ResourceBarChangePresentation presentation)
    {
        float target = Sanitize(value);
        StopFillRoutine();

        float duration = presentation == ResourceBarChangePresentation.Gain
            ? gainDuration
            : presentation == ResourceBarChangePresentation.Spend ? spendDuration : 0f;

        if (!isActiveAndEnabled || duration <= 0f || presentation == ResourceBarChangePresentation.Snap)
        {
            ApplyFill(target);
        }
        else
        {
            fillRoutine = StartCoroutine(FillRoutine(FillAmount01, target, duration));
        }

        if (presentation == ResourceBarChangePresentation.Gain
            || presentation == ResourceBarChangePresentation.Spend)
        {
            PlayPulse(presentation == ResourceBarChangePresentation.Gain ? 1.035f : 0.985f);
        }
        else if (presentation == ResourceBarChangePresentation.Cleared)
        {
            // Death/clear is deliberately quiet: it must not read as ordinary spending.
            ApplyPulse(0f, 1f);
        }
    }

    public void ConfigureDurations(float gain, float spend, float pulse)
    {
        gainDuration = Mathf.Max(0f, gain);
        spendDuration = Mathf.Max(0f, spend);
        pulseDuration = Mathf.Max(0f, pulse);
    }

    private IEnumerator FillRoutine(float start, float target, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float eased = t * t * (3f - 2f * t);
            ApplyFill(Mathf.Lerp(start, target, eased));
            yield return null;
        }

        ApplyFill(target);
        fillRoutine = null;
    }

    private void PlayPulse(float peakScale)
    {
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
        }

        if (!isActiveAndEnabled || pulseDuration <= 0f)
        {
            ApplyPulse(0f, 1f);
            pulseRoutine = null;
            return;
        }

        pulseRoutine = StartCoroutine(PulseRoutine(peakScale));
    }

    private IEnumerator PulseRoutine(float peakScale)
    {
        float elapsed = 0f;
        while (elapsed < pulseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = pulseDuration > 0f ? Mathf.Clamp01(elapsed / pulseDuration) : 1f;
            float wave = Mathf.Sin(t * Mathf.PI);
            ApplyPulse(wave, Mathf.Lerp(1f, peakScale, wave));
            yield return null;
        }

        ApplyPulse(0f, 1f);
        pulseRoutine = null;
    }

    private void ApplyFill(float value)
    {
        FillAmount01 = Sanitize(value);

        if (maskedFill != null)
        {
            maskedFill.SetFill(FillAmount01);
            return;
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = FillAmount01;
        }
    }

    private void ApplyPulse(float alpha, float scale)
    {
        if (edgePulse != null)
        {
            edgePulse.alpha = Mathf.Clamp01(alpha);
            edgePulse.interactable = false;
            edgePulse.blocksRaycasts = false;
        }

        if (animatedRoot != null)
        {
            animatedRoot.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private void StopPresentation()
    {
        StopFillRoutine();
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }
    }

    private void StopFillRoutine()
    {
        if (fillRoutine != null)
        {
            StopCoroutine(fillRoutine);
            fillRoutine = null;
        }
    }

    private static float Sanitize(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
    }
}
