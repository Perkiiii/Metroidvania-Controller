using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared presentation-only presenter for a confirmed tab whose authoritative gameplay owner does
/// not exist yet: Tools, Satchel, Recipes, Tasks, Journal, and Map in Package A2.
///
/// It owns no gameplay state, no data source, no persistence, and no subscriptions — every
/// difference between the six tabs is authored in their prefabs (copy, motif, accent, layout), not
/// duplicated in six identical scripts. When a tab's real domain owner is approved it gets its own
/// focused presenter (like <see cref="GearScreen"/>) and this component is simply swapped out of
/// that tab's prefab; <see cref="IGameplayMenuTab"/> does not change.
///
/// The authored copy must read as an intentional player-facing state. It must never contain
/// developer wording ("not implemented", "coming soon"), error styling, unknown totals, capacity,
/// silhouettes, or any invented content.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayMenuEmptyTabView : MonoBehaviour, IGameplayMenuTab
{
    [Tooltip("Authored content for this tab. Deactivated whenever the tab is not the active one.")]
    [SerializeField] private GameObject contentRoot;

    [Header("Authored copy (presentation only)")]
    [SerializeField] private Text titleLabel;
    [SerializeField] private Text bodyLabel;

    [Tooltip("Optional decorative motif. Never a data-bearing icon.")]
    [SerializeField] private Graphic motif;

    [Header("Selection")]
    [Tooltip("Optional. Package A2 empty states expose no content selection, so this stays unset " +
        "and the Gameplay Menu keeps focus on the tab rail.")]
    [SerializeField] private Selectable firstSelection;

    public Selectable FirstSelection => firstSelection;

    public bool HasOpenModal => false;

    /// <summary>Editor/validator accessor: authored title copy, or null when unassigned.</summary>
    public string AuthoredTitle => titleLabel != null ? titleLabel.text : null;

    /// <summary>Editor/validator accessor: authored body copy, or null when unassigned.</summary>
    public string AuthoredBody => bodyLabel != null ? bodyLabel.text : null;

    public GameObject ContentRoot => contentRoot;

    public void Show()
    {
        if (contentRoot != null)
        {
            contentRoot.SetActive(true);
        }
    }

    public void Hide()
    {
        if (contentRoot != null)
        {
            contentRoot.SetActive(false);
        }
    }

    public bool HandleBackInternally()
    {
        // No nested pages exist in Package A2, so Back always falls through to the root close.
        return false;
    }

    /// <summary>
    /// Sandbox/authoring seam for previewing alternative copy without editing the prefab. Never
    /// called by production code.
    /// </summary>
    public void SetAuthoredCopy(string title, string body)
    {
        if (titleLabel != null && title != null) titleLabel.text = title;
        if (bodyLabel != null && body != null) bodyLabel.text = body;
    }

    /// <summary>Sandbox/authoring seam for previewing the tab with and without its motif.</summary>
    public void SetMotifVisible(bool visible)
    {
        if (motif != null) motif.gameObject.SetActive(visible);
    }
}
