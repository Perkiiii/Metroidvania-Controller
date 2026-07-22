using UnityEngine;
using UnityEngine.UI;

/// <summary>Artwork-agnostic fill element for the persistent resource bar.</summary>
public sealed class ResourceBarView : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    public float FillAmount01 { get; private set; }

    public void SetFill(float value)
    {
        FillAmount01 = Mathf.Clamp01(value);
        if (fillImage != null)
            fillImage.fillAmount = FillAmount01;
    }
}
