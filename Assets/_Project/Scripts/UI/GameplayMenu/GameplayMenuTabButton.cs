using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One control in the top-centred tab strip. Owns only its own presentation: the active-tab
/// treatment is deliberately separate from UGUI's selected/highlighted treatment so "which tab am I
/// looking at" stays legible even while focus sits in the content area or on another control.
///
/// The cell is authored as an underline plus a layout-driven glyph/label stack, so
/// <see cref="SetCompact"/> only has to hide the label and change the cell's preferred width — the
/// layout group re-centres the glyph on its own. No code positions anything, which keeps the cell
/// fully editable by the UI/UX partner.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayMenuTabButton : MonoBehaviour,
    ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    [Tooltip("Underline/marker shown only while this tab is the active one.")]
    [SerializeField] private Graphic activeIndicator;

    [Tooltip("This tab's authored accent glyph. Its colour is authored per tab; only its alpha is " +
        "driven here, so the accent hue is never overwritten by code.")]
    [SerializeField] private Graphic glyph;
    [Tooltip("Independent focus/hover channel. Active-tab identity never uses this object.")]
    [SerializeField] private Graphic focusIndicator;

    [Tooltip("Drives the cell's width in the strip's HorizontalLayoutGroup.")]
    [SerializeField] private LayoutElement layoutElement;

    [Header("Active-tab presentation")]
    [SerializeField] private Color activeLabelColor = new Color(0.96f, 0.90f, 0.76f, 1f);
    [SerializeField] private Color inactiveLabelColor = new Color(0.62f, 0.63f, 0.58f, 1f);
    [SerializeField, Range(0f, 1f)] private float activeGlyphAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float inactiveGlyphAlpha = 0.55f;

    [Header("Strip sizing")]
    [Tooltip("Cell width when the strip has room for glyph + title.")]
    [SerializeField, Min(1f)] private float expandedWidth = 150f;
    [Tooltip("Cell width at narrow aspect ratios. Every tab stays visible — only the per-tab title " +
        "collapses, and the active tab's name is still shown by the frame's separate title label.")]
    [SerializeField, Min(1f)] private float compactWidth = 72f;

    /// <summary>Raised when the player activates this tab control (click or Submit).</summary>
    public event Action Clicked;

    public Button Button => button;

    public Selectable Selectable => button;

    public bool IsActiveTab { get; private set; }

    /// <summary>True while the per-tab title is collapsed for a narrow strip.</summary>
    public bool IsCompact { get; private set; }

    /// <summary>Width this cell wants when not compact; the strip uses it to decide whether it fits.</summary>
    public float ExpandedWidth => expandedWidth;
    public bool HasFocus { get; private set; }
    public bool IsHovered { get; private set; }

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(HandleClicked);
        }

        ApplyActivePresentation();
        ApplyCompactPresentation();
        ApplyFocusPresentation();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }
    }

    public void SetActiveTab(bool value)
    {
        IsActiveTab = value;
        ApplyActivePresentation();
    }

    /// <summary>Sets the player-facing tab name. Presentation only.</summary>
    public void SetLabel(string text)
    {
        if (label != null && !string.IsNullOrEmpty(text))
        {
            label.text = text;
        }
    }

    /// <summary>
    /// Collapses this cell to glyph-only. Never deactivates the cell itself: all five tabs stay
    /// visible and reachable at every supported aspect ratio.
    /// </summary>
    public void SetCompact(bool compact)
    {
        IsCompact = compact;
        ApplyCompactPresentation();
    }

    private void ApplyCompactPresentation()
    {
        if (label != null)
        {
            label.gameObject.SetActive(!IsCompact);
        }

        if (layoutElement != null)
        {
            layoutElement.preferredWidth = IsCompact ? compactWidth : expandedWidth;
        }
    }

    private void ApplyActivePresentation()
    {
        if (activeIndicator != null)
        {
            activeIndicator.gameObject.SetActive(IsActiveTab);
        }

        if (label != null)
        {
            label.color = IsActiveTab ? activeLabelColor : inactiveLabelColor;
        }

        if (glyph != null)
        {
            Color authored = glyph.color;
            authored.a = IsActiveTab ? activeGlyphAlpha : inactiveGlyphAlpha;
            glyph.color = authored;
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        HasFocus = true;
        ApplyFocusPresentation();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        HasFocus = false;
        ApplyFocusPresentation();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsHovered = true;
        ApplyFocusPresentation();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsHovered = false;
        ApplyFocusPresentation();
    }

    private void ApplyFocusPresentation()
    {
        if (focusIndicator != null)
        {
            focusIndicator.gameObject.SetActive(HasFocus || IsHovered);
        }
    }

    private void HandleClicked()
    {
        Clicked?.Invoke();
    }
}
