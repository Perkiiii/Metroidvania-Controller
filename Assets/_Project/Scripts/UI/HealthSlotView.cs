using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum HealthSlotVisualState
{
    Empty,
    Filled,
    Bonus
}

public enum HealthSlotFeedback
{
    None,
    Damage,
    Heal,
    Bonus
}

/// <summary>Layered presentation element for one normal or bonus health slot.</summary>
[DisallowMultipleComponent]
public sealed class HealthSlotView : MonoBehaviour
{
    [SerializeField] private GameObject emptyVisual;
    [SerializeField] private GameObject filledVisual;
    [SerializeField] private GameObject bonusVisual;
    [SerializeField] private Image stateImage;
    [SerializeField] private Color emptyColor = Color.gray;
    [SerializeField] private Color filledColor = Color.white;
    [SerializeField] private Color bonusColor = Color.yellow;
    [SerializeField] private CanvasGroup feedbackOverlay;
    [SerializeField] private RectTransform animatedRoot;
    [SerializeField, Min(0f)] private float feedbackDuration = 0.16f;

    public HealthSlotVisualState State { get; private set; }
    public HealthSlotFeedback LastFeedback { get; private set; }

    private Coroutine feedbackRoutine;

    private void OnDisable()
    {
        StopFeedback();
        ApplyFeedbackVisual(0f, 1f);
    }

    public void SetState(HealthSlotVisualState state)
    {
        SetState(state, HealthSlotFeedback.None);
    }

    public void SetState(HealthSlotVisualState state, HealthSlotFeedback feedback)
    {
        State = state;
        SetActive(emptyVisual, state == HealthSlotVisualState.Empty);
        SetActive(filledVisual, state == HealthSlotVisualState.Filled);
        SetActive(bonusVisual, state == HealthSlotVisualState.Bonus);

        if (stateImage != null)
            stateImage.color = state == HealthSlotVisualState.Empty ? emptyColor
                : state == HealthSlotVisualState.Bonus ? bonusColor : filledColor;

        if (feedback != HealthSlotFeedback.None)
        {
            PlayFeedback(feedback);
        }
    }

    public void ConfigureFeedbackDuration(float seconds)
    {
        feedbackDuration = Mathf.Max(0f, seconds);
    }

    private void PlayFeedback(HealthSlotFeedback feedback)
    {
        StopFeedback();
        LastFeedback = feedback;

        if (!isActiveAndEnabled || feedbackDuration <= 0f)
        {
            ApplyFeedbackVisual(0f, 1f);
            return;
        }

        feedbackRoutine = StartCoroutine(FeedbackRoutine(feedback));
    }

    private IEnumerator FeedbackRoutine(HealthSlotFeedback feedback)
    {
        float peakScale = feedback == HealthSlotFeedback.Damage ? 0.90f : 1.12f;
        float elapsed = 0f;
        while (elapsed < feedbackDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = feedbackDuration > 0f ? Mathf.Clamp01(elapsed / feedbackDuration) : 1f;
            float wave = Mathf.Sin(t * Mathf.PI);
            ApplyFeedbackVisual(wave, Mathf.Lerp(1f, peakScale, wave));
            yield return null;
        }

        ApplyFeedbackVisual(0f, 1f);
        feedbackRoutine = null;
    }

    private void ApplyFeedbackVisual(float alpha, float scale)
    {
        if (feedbackOverlay != null)
        {
            feedbackOverlay.alpha = Mathf.Clamp01(alpha);
            feedbackOverlay.interactable = false;
            feedbackOverlay.blocksRaycasts = false;
        }

        if (animatedRoot != null)
        {
            animatedRoot.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private void StopFeedback()
    {
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null)
            target.SetActive(value);
    }
}
