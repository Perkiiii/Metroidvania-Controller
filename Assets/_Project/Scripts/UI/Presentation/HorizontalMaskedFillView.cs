using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only horizontal fill that reveals full-width artwork through a left-anchored
/// <see cref="RectMask2D"/> viewport. The artwork keeps its authored width, so gradients,
/// highlights, texture, and ornament are cropped rather than compressed.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class HorizontalMaskedFillView : MonoBehaviour
{
    [Tooltip("Full bar width. When omitted, the viewport parent RectTransform is used.")]
    [SerializeField] private RectTransform track;

    [Tooltip("Left-anchored RectMask2D viewport whose width is driven by the normalized value.")]
    [SerializeField] private RectTransform viewport;

    [Tooltip("Artwork revealed by the viewport. It always retains the full track width.")]
    [SerializeField] private RectTransform artwork;

    [Tooltip("Optional decoration placed at the visible fill edge and hidden at zero.")]
    [SerializeField] private RectTransform leadingEdge;

    private float fillAmount01;

    public float FillAmount01 => fillAmount01;
    public float VisibleWidth { get; private set; }
    public float ArtworkWidth { get; private set; }
    public bool HasValidDependencies => ResolveTrack() != null && ResolveViewport() != null && artwork != null;

    private void Awake()
    {
        ApplyFill();
    }

    private void OnEnable()
    {
        ApplyFill();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled)
        {
            ApplyFill();
        }
    }

    /// <summary>Runtime/test composition seam. Production authors these references in prefabs.</summary>
    public void Configure(
        RectTransform trackRect,
        RectTransform viewportRect,
        RectTransform artworkRect,
        RectTransform leadingEdgeRect = null)
    {
        track = trackRect;
        viewport = viewportRect;
        artwork = artworkRect;
        leadingEdge = leadingEdgeRect;
        ApplyFill();
    }

    public void SetFill(float value)
    {
        fillAmount01 = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        ApplyFill();
    }

    private void ApplyFill()
    {
        RectTransform trackRect = ResolveTrack();
        RectTransform viewportRect = ResolveViewport();
        if (trackRect == null || viewportRect == null)
        {
            VisibleWidth = 0f;
            ArtworkWidth = 0f;
            SetLeadingEdgeVisible(false);
            return;
        }

        float trackWidth = Mathf.Max(0f, trackRect.rect.width);
        VisibleWidth = trackWidth * fillAmount01;
        ArtworkWidth = trackWidth;

        viewportRect.anchorMin = new Vector2(0f, viewportRect.anchorMin.y);
        viewportRect.anchorMax = new Vector2(0f, viewportRect.anchorMax.y);
        viewportRect.pivot = new Vector2(0f, viewportRect.pivot.y);
        viewportRect.anchoredPosition = new Vector2(0f, viewportRect.anchoredPosition.y);
        viewportRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, VisibleWidth);

        if (artwork != null)
        {
            artwork.anchorMin = new Vector2(0f, artwork.anchorMin.y);
            artwork.anchorMax = new Vector2(0f, artwork.anchorMax.y);
            artwork.pivot = new Vector2(0f, artwork.pivot.y);
            artwork.anchoredPosition = new Vector2(0f, artwork.anchoredPosition.y);
            artwork.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ArtworkWidth);
        }

        if (leadingEdge != null)
        {
            leadingEdge.anchorMin = new Vector2(0f, leadingEdge.anchorMin.y);
            leadingEdge.anchorMax = new Vector2(0f, leadingEdge.anchorMax.y);
            leadingEdge.anchoredPosition = new Vector2(VisibleWidth, leadingEdge.anchoredPosition.y);
        }

        SetLeadingEdgeVisible(fillAmount01 > 0f && trackWidth > 0f);
    }

    private RectTransform ResolveTrack()
    {
        if (track != null)
        {
            return track;
        }

        RectTransform viewportRect = ResolveViewport();
        return viewportRect != null ? viewportRect.parent as RectTransform : null;
    }

    private RectTransform ResolveViewport()
    {
        return viewport != null ? viewport : transform as RectTransform;
    }

    private void SetLeadingEdgeVisible(bool visible)
    {
        if (leadingEdge != null && leadingEdge.gameObject.activeSelf != visible)
        {
            leadingEdge.gameObject.SetActive(visible);
        }
    }
}
