using UnityEngine.UI;

/// <summary>
/// Narrow lifecycle contract every Gameplay Menu tab implements. Deliberately limited to
/// presentation lifecycle, Back routing, and first selection: it exposes no gameplay owner, save
/// API, item/recipe/task/map data, input-reader access, or generic window behaviour. This is a
/// fixed-tab lifecycle contract, not a window framework.
///
/// A future domain owner (inventory, recipes, tasks, journal, map) is bound inside the concrete
/// tab presenter, never by widening this interface. See Docs/FeatureSpecs/GameplayMenu.md.
/// </summary>
public interface IGameplayMenuTab
{
    /// <summary>
    /// The tab's preferred content selection, or null when the tab has no selectable content
    /// (every empty-state tab in Package A2). Null is expected and safe — the owning screen falls
    /// back to the registration fallback, then the tab button, then the global Close control.
    /// </summary>
    Selectable FirstSelection { get; }

    /// <summary>Activates this tab's authored content. Must be idempotent.</summary>
    void Show();

    /// <summary>Deactivates this tab's authored content. Must be idempotent.</summary>
    void Hide();

    /// <summary>
    /// Back/Cancel while this tab is active. Return true only when the tab consumed it (e.g. it
    /// closed its own nested page); false lets <see cref="UIFlowController"/> close the whole root.
    /// </summary>
    bool HandleBackInternally();

    /// <summary>True while this tab owns a nested layer that must close before the root does.</summary>
    bool HasOpenModal { get; }
}

/// <summary>
/// Optional presentation-only bridge for a tab with selectable content below the shared strip.
/// It lets the root supply the active strip button as the content's upward return target without
/// widening the core tab lifecycle or making the tab search its parent hierarchy.
/// </summary>
public interface IGameplayMenuTabNavigation
{
    void SetTabStripReturnTarget(Selectable target);
}
