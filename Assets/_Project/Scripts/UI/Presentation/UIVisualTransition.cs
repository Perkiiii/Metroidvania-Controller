using System.Collections;
using UnityEngine;

/// <summary>
/// Small unscaled-time fade/scale presenter for an already-valid UI root. It never owns root
/// lifecycle, input, pause, or raycast policy.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIVisualTransition : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform animatedRoot;
    [SerializeField, Min(0f)] private float showDuration = 0.14f;
    [SerializeField, Range(0.8f, 1f)] private float hiddenScale = 0.975f;

    private Coroutine routine;

    private void OnDisable()
    {
        StopTransition();
        Apply(1f, 1f);
    }

    public void PlayShow()
    {
        StopTransition();

        if (!isActiveAndEnabled || showDuration <= 0f)
        {
            Apply(1f, 1f);
            return;
        }

        routine = StartCoroutine(ShowRoutine());
    }

    public void SnapVisible()
    {
        StopTransition();
        Apply(1f, 1f);
    }

    public void ConfigureDuration(float seconds)
    {
        showDuration = Mathf.Max(0f, seconds);
    }

    private IEnumerator ShowRoutine()
    {
        float elapsed = 0f;
        Apply(0f, hiddenScale);
        while (elapsed < showDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = showDuration > 0f ? Mathf.Clamp01(elapsed / showDuration) : 1f;
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            Apply(eased, Mathf.Lerp(hiddenScale, 1f, eased));
            yield return null;
        }

        Apply(1f, 1f);
        routine = null;
    }

    private void Apply(float alpha, float scale)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        if (animatedRoot != null)
        {
            animatedRoot.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private void StopTransition()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }
}
