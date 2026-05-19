using System.Collections;
using UnityEngine;

// Drives _FlashAmount and _FlashColor on the attached SpriteRenderer via MaterialPropertyBlock.
// Uses unscaled time throughout so flashes play correctly during hit stop (Time.timeScale = 0).
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteFlash : MonoBehaviour
{
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorId  = Shader.PropertyToID("_FlashColor");

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock block;
    private Coroutine flashCoroutine;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnDisable()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        SetFlash(0f, Color.white);
    }

    // Standard hit flash: ~85% white, Silksong-reference timing.
    [ContextMenu("Flash Hit")]
    public void FlashHit()
    {
        Flash(Color.white, 0.85f, 0.07f, 0.05f);
    }

    // amount: peak flash intensity (0-1). holdDuration and fadeDuration in real seconds.
    public void Flash(Color color, float amount, float holdDuration, float fadeDuration)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        flashCoroutine = StartCoroutine(FlashRoutine(color, amount, holdDuration, fadeDuration));
    }

    private IEnumerator FlashRoutine(Color color, float amount, float holdDuration, float fadeDuration)
    {
        SetFlash(amount, color);
        yield return new WaitForSecondsRealtime(holdDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetFlash(amount * (1f - Mathf.Clamp01(elapsed / fadeDuration)), color);
            yield return null;
        }

        SetFlash(0f, color);
        flashCoroutine = null;
    }

    private void SetFlash(float amount, Color color)
    {
        spriteRenderer.GetPropertyBlock(block);
        block.SetFloat(FlashAmountId, amount);
        block.SetColor(FlashColorId, color);
        spriteRenderer.SetPropertyBlock(block);
    }
}
