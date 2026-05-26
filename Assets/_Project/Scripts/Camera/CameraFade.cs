using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class CameraFade : MonoBehaviour
{
    [FormerlySerializedAs("fadeGroup")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float defaultFadeOutDuration = 0.25f;
    [SerializeField] private float defaultFadeInDuration = 0.25f;
    [SerializeField] private bool startClear = true;

    public bool IsFading { get; private set; }

    private Coroutine activeFade;

    private void Awake()
    {
        if (!EnsureCanvasGroup())
            return;

        canvasGroup.interactable = false;

        if (startClear)
            ApplyAlpha(0f);
        else
            ApplyAlpha(1f);
    }

    public IEnumerator FadeOut(float duration = -1f)
    {
        CancelActiveFade();

        if (!EnsureCanvasGroup())
            yield break;

        float resolvedDuration = duration < 0f ? defaultFadeOutDuration : duration;
        if (resolvedDuration <= 0f)
        {
            SetBlackInternal();
            yield break;
        }

        activeFade = StartCoroutine(FadeRoutine(0f, 1f, resolvedDuration));
        yield return activeFade;
    }

    public IEnumerator FadeIn(float duration = -1f)
    {
        CancelActiveFade();

        if (!EnsureCanvasGroup())
            yield break;

        float resolvedDuration = duration < 0f ? defaultFadeInDuration : duration;
        if (resolvedDuration <= 0f)
        {
            SetClearInternal();
            yield break;
        }

        activeFade = StartCoroutine(FadeRoutine(1f, 0f, resolvedDuration));
        yield return activeFade;
    }

    public void SetBlack()
    {
        CancelActiveFade();
        if (!EnsureCanvasGroup())
            return;

        SetBlackInternal();
    }

    public void SetClear()
    {
        CancelActiveFade();
        if (!EnsureCanvasGroup())
            return;

        SetClearInternal();
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        IsFading = true;
        ApplyAlpha(from);

        // Directional curves:
        //   Fade-in  (1 → 0, revealing the scene): SmoothStep — gentle ease-in-out so the
        //            reveal starts and ends gradually.
        //   Fade-out (0 → 1, going to black): quadratic ease-out — screen darkens immediately
        //            on trigger, then eases into full black. Keeps the cut feeling responsive
        //            without ending harshly.
        bool fadingIn = to < from;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = fadingIn
                ? Mathf.SmoothStep(0f, 1f, t)
                : 1f - (1f - t) * (1f - t);
            ApplyAlpha(Mathf.Lerp(from, to, curvedT));
            yield return null;
        }

        ApplyAlpha(to);
        IsFading = false;
        activeFade = null;
    }

    private bool EnsureCanvasGroup()
    {
        if (canvasGroup != null)
            return true;

        if (!TryGetComponent(out canvasGroup))
        {
            Debug.LogError("[CameraFade] Missing CanvasGroup. Assign one on the CameraFade component or add a CanvasGroup to the same GameObject.", this);
            return false;
        }

        return true;
    }

    private void CancelActiveFade()
    {
        if (activeFade != null)
        {
            StopCoroutine(activeFade);
            activeFade = null;
        }

        IsFading = false;
    }

    private void SetBlackInternal()
    {
        ApplyAlpha(1f);
        IsFading = false;
    }

    private void SetClearInternal()
    {
        ApplyAlpha(0f);
        IsFading = false;
    }

    private void ApplyAlpha(float alpha)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = alpha;
        canvasGroup.blocksRaycasts = alpha > 0f;
        canvasGroup.interactable = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (canvasGroup == null && GetComponent<CanvasGroup>() == null)
            Debug.LogWarning("[CameraFade] No CanvasGroup assigned or present on the same GameObject.", this);
    }
#endif
}
