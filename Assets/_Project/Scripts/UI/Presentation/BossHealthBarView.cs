using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only boss bar. It owns masked-fill interpolation, delayed damage presentation,
/// name/icon rendering, and show visuals. It never retains encounter or enemy references.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossHealthBarView : MonoBehaviour
{
    [Header("Visibility")]
    [SerializeField] private CanvasGroup visibilityGroup;
    [SerializeField] private RectTransform animatedRoot;

    [Header("Content")]
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private Image iconImage;
    [SerializeField] private HorizontalMaskedFillView mainFill;
    [SerializeField] private HorizontalMaskedFillView trailingFill;

    [Header("Unscaled presentation")]
    [SerializeField, Min(0f)] private float showDuration = 0.18f;
    [SerializeField, Min(0f)] private float damageMainDuration = 0.06f;
    [SerializeField, Min(0f)] private float trailingDelay = 0.20f;
    [SerializeField, Min(0f)] private float trailingCatchupDuration = 0.28f;
    [SerializeField, Range(0.8f, 1f)] private float hiddenScale = 0.96f;

    private Coroutine showRoutine;
    private Coroutine fillRoutine;
    private float mainValue;
    private float trailingValue;

    public float MainFillAmount01 => mainValue;
    public float TrailingFillAmount01 => trailingValue;
    public string DisplayName => nameLabel != null ? nameLabel.text : string.Empty;
    public bool IconVisible => iconImage != null && iconImage.enabled;
    public bool IsVisible => visibilityGroup == null ? gameObject.activeInHierarchy : visibilityGroup.alpha > 0f;

    private void OnDisable()
    {
        StopPresentationRoutines();
        ApplyMain(mainValue);
        ApplyTrailing(trailingValue);
    }

    public void ConfigureDurations(float show, float mainDamage, float delay, float trailingCatchup)
    {
        showDuration = Mathf.Max(0f, show);
        damageMainDuration = Mathf.Max(0f, mainDamage);
        trailingDelay = Mathf.Max(0f, delay);
        trailingCatchupDuration = Mathf.Max(0f, trailingCatchup);
    }

    public void ShowSnapshot(string displayName, Sprite icon, float fillAmount)
    {
        StopPresentationRoutines();

        if (nameLabel != null)
        {
            nameLabel.text = displayName ?? string.Empty;
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        float normalized = Sanitize(fillAmount);
        ApplyMain(normalized);
        ApplyTrailing(normalized);
        ApplyRaycastState(false);

        if (!isActiveAndEnabled || showDuration <= 0f)
        {
            ApplyVisibility(1f, 1f);
            return;
        }

        showRoutine = StartCoroutine(ShowRoutine());
    }

    public void SetHealth(float fillAmount)
    {
        float target = Sanitize(fillAmount);
        StopFillRoutine();

        if (target >= mainValue)
        {
            // Healing and capacity increases must never resemble delayed damage.
            ApplyMain(target);
            ApplyTrailing(target);
            return;
        }

        if (!isActiveAndEnabled || (damageMainDuration <= 0f && trailingDelay <= 0f && trailingCatchupDuration <= 0f))
        {
            ApplyMain(target);
            ApplyTrailing(target);
            return;
        }

        fillRoutine = StartCoroutine(DamageRoutine(target));
    }

    public void HideImmediate()
    {
        StopPresentationRoutines();
        ApplyMain(0f);
        ApplyTrailing(0f);
        ApplyVisibility(0f, hiddenScale);
        ApplyRaycastState(false);

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (nameLabel != null)
        {
            nameLabel.text = string.Empty;
        }
    }

    private IEnumerator ShowRoutine()
    {
        float elapsed = 0f;
        ApplyVisibility(0f, hiddenScale);
        while (elapsed < showDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = showDuration > 0f ? Mathf.Clamp01(elapsed / showDuration) : 1f;
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            ApplyVisibility(eased, Mathf.Lerp(hiddenScale, 1f, eased));
            yield return null;
        }

        ApplyVisibility(1f, 1f);
        showRoutine = null;
    }

    private IEnumerator DamageRoutine(float target)
    {
        float mainStart = mainValue;
        float elapsed = 0f;
        while (elapsed < damageMainDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = damageMainDuration > 0f ? Mathf.Clamp01(elapsed / damageMainDuration) : 1f;
            ApplyMain(Mathf.Lerp(mainStart, target, t));
            yield return null;
        }
        ApplyMain(target);

        if (trailingDelay > 0f)
        {
            float wait = 0f;
            while (wait < trailingDelay)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        float trailingStart = trailingValue;
        elapsed = 0f;
        while (elapsed < trailingCatchupDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = trailingCatchupDuration > 0f
                ? Mathf.Clamp01(elapsed / trailingCatchupDuration)
                : 1f;
            float eased = t * t * (3f - 2f * t);
            ApplyTrailing(Mathf.Lerp(trailingStart, target, eased));
            yield return null;
        }

        ApplyTrailing(target);
        fillRoutine = null;
    }

    private void ApplyMain(float value)
    {
        mainValue = Sanitize(value);
        if (mainFill != null)
        {
            mainFill.SetFill(mainValue);
        }
    }

    private void ApplyTrailing(float value)
    {
        trailingValue = Sanitize(value);
        if (trailingFill != null)
        {
            trailingFill.SetFill(trailingValue);
        }
    }

    private void ApplyVisibility(float alpha, float scale)
    {
        if (visibilityGroup != null)
        {
            visibilityGroup.alpha = Mathf.Clamp01(alpha);
        }

        if (animatedRoot != null)
        {
            animatedRoot.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private void ApplyRaycastState(bool interactive)
    {
        if (visibilityGroup != null)
        {
            visibilityGroup.interactable = interactive;
            visibilityGroup.blocksRaycasts = interactive;
        }
    }

    private void StopPresentationRoutines()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        StopFillRoutine();
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
