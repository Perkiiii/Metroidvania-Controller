using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One authored Gameplay Menu tab: its identity, strip button, presentation owner, and an optional
/// authored selection fallback. Authored as a fixed seven-element list on
/// <see cref="GameplayMenuScreen"/>.
///
/// There is deliberately no availability/hidden flag: all seven confirmed tabs are always visible.
/// Missing content is expressed by the view's empty state, never by filtering registrations.
/// </summary>
[Serializable]
public sealed class GameplayMenuTabRegistration
{
    [SerializeField] private GameplayMenuTabId id;

    [Tooltip("Player-facing tab name. Leave empty to use the tab ID name.")]
    [SerializeField] private string displayName;

    [SerializeField] private GameplayMenuTabButton tabButton;

    [Tooltip("Must implement IGameplayMenuTab.")]
    [SerializeField] private MonoBehaviour tabViewBehaviour;

    [Tooltip("Optional authored selection used when the view offers none (empty-state tabs). " +
        "The tab button and the global Close control remain the final fallbacks.")]
    [SerializeField] private Selectable firstSelectionFallback;

    public GameplayMenuTabRegistration()
    {
    }

    public GameplayMenuTabRegistration(
        GameplayMenuTabId id,
        GameplayMenuTabButton tabButton,
        MonoBehaviour tabViewBehaviour,
        Selectable firstSelectionFallback = null,
        string displayName = null)
    {
        this.id = id;
        this.tabButton = tabButton;
        this.tabViewBehaviour = tabViewBehaviour;
        this.firstSelectionFallback = firstSelectionFallback;
        this.displayName = displayName;
    }

    public GameplayMenuTabId Id => id;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id.ToString() : displayName;

    public GameplayMenuTabButton TabButton => tabButton;

    public MonoBehaviour TabViewBehaviour => tabViewBehaviour;

    public Selectable FirstSelectionFallback => firstSelectionFallback;

    /// <summary>The resolved tab presenter, or null when the assigned behaviour is missing/invalid.</summary>
    public IGameplayMenuTab View => tabViewBehaviour as IGameplayMenuTab;

    /// <summary>Transform that owns this tab's authored content, used for selection-ownership checks.</summary>
    public Transform ViewTransform => tabViewBehaviour != null ? tabViewBehaviour.transform : null;
}
