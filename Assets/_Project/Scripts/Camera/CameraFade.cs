using System.Collections;
using UnityEngine;

public sealed class CameraFade : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private float defaultDuration = 0.4f;

    public bool IsFading { get; private set; }

    private void Awake()
    {
        if (fadeGroup != null)
            fadeGroup.alpha = 0f;
    }

    public IEnumerator FadeOut(float duration = -1f)
    {
        yield return Fade(0f, 1f, duration < 0f ? defaultDuration : duration);
    }

    public IEnumerator FadeIn(float duration = -1f)
    {
        yield return Fade(1f, 0f, duration < 0f ? defaultDuration : duration);
    }

    public Coroutine FadeToBlack(float duration = -1f)
    {
        return StartCoroutine(FadeOut(duration));
    }

    public Coroutine FadeToClear(float duration = -1f)
    {
        return StartCoroutine(FadeIn(duration));
    }

    public void FadeOutImmediate()
    {
        StopAllCoroutines();
        IsFading = false;
        if (fadeGroup != null) fadeGroup.alpha = 1f;
    }

    public void FadeInImmediate()
    {
        StopAllCoroutines();
        IsFading = false;
        if (fadeGroup != null) fadeGroup.alpha = 0f;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeGroup == null) yield break;

        IsFading = true;
        fadeGroup.alpha = from;

        if (duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
        }

        fadeGroup.alpha = to;
        IsFading = false;
    }
}
