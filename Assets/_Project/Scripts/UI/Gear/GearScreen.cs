using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// The Gear tab: the acquired physical permanent progression possessions.
///
/// Ownership boundary. <see cref="PlayerAbilityState"/> is the sole authority for what the player
/// owns; this screen only reads <see cref="PlayerAbilityState.IsUnlocked"/> and observes
/// <see cref="PlayerAbilityState.AbilityChanged"/>. It never calls Unlock, Lock, SetUnlocked, or
/// ResetToDefaults, never touches HeroAbilityConfig or any tuning value, and never saves.
///
/// Undiscovered Gear is absent, not hinted: a locked ability produces no entry, placeholder,
/// silhouette, anchor, or count. An unlocked ability with no approved definition is omitted safely
/// and reported once for development.
///
/// <see cref="PlayerAbilityState.AbilityChanged"/> is an ownership-changed signal, never an
/// acquisition notification — a save being applied must not read as "you just found something".
/// See Docs/FeatureSpecs/Gear.md.
/// </summary>
[DisallowMultipleComponent]
public sealed class GearScreen : MonoBehaviour, IGameplayMenuTab, IGameplayMenuTabNavigation
{
    [Tooltip("Authored content for this tab. Deactivated whenever Gear is not the active tab.")]
    [SerializeField] private GameObject contentRoot;

    [Header("Authoritative ownership source (read-only)")]
    [Tooltip("Production PlayerAbilityState asset. Gear reads it and never mutates it.")]
    [SerializeField] private PlayerAbilityState abilityState;

    [Header("Display data")]
    [SerializeField] private GearDisplayCatalog catalog;

    [Header("Collection")]
    [Tooltip("Collection + details composition. Shown only while at least one Gear is acquired, " +
        "so a true new game presents the authored empty state instead of an empty frame.")]
    [SerializeField] private GameObject populatedRoot;

    [SerializeField] private RectTransform entryContainer;
    [SerializeField] private GearEntryView entryPrefab;

    [Header("Details / empty state")]
    [SerializeField] private GearDetailsPanel detailsPanel;

    [Tooltip("Authored zero-entry presentation. No unknown totals, slots, or silhouettes.")]
    [SerializeField] private GameObject emptyStateRoot;

    [Header("Navigation")]
    [FormerlySerializedAs("railReturnTarget")]
    [Tooltip("Control focus returns to when moving Up from the first entry — normally the " +
        "active Gear button in the shared top strip.")]
    [SerializeField] private Selectable stripReturnTarget;

    private readonly List<GearEntryView> activeEntries = new List<GearEntryView>();
    private readonly HashSet<AbilityId> loggedMissingDefinitions = new HashSet<AbilityId>();

    private bool isShown;
    private bool subscribed;
    private string selectedStableKey;

    /// <summary>First acquired entry, or null when the player owns no mapped Gear yet.</summary>
    public Selectable FirstSelection
    {
        get
        {
            for (int i = 0; i < activeEntries.Count; i++)
            {
                if (UISelectionUtility.IsSelectable(activeEntries[i]?.Selectable))
                {
                    return activeEntries[i].Selectable;
                }
            }

            return null;
        }
    }

    public bool HasOpenModal => false;

    /// <summary>Number of acquired Gear entries currently presented.</summary>
    public int EntryCount => activeEntries.Count;

    /// <summary>Stable key of the selected entry, or null in the empty state.</summary>
    public string SelectedStableKey => selectedStableKey;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Build once up front so FirstSelection is meaningful even before the first Show.
        Rebuild();
        if (contentRoot != null)
        {
            contentRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    /// <summary>
    /// Runtime composition seam for the UI Sandbox and EditMode tests: binds an isolated cloned
    /// ability state and a fixture catalogue. Production wires both in the Inspector.
    /// </summary>
    public void Configure(PlayerAbilityState state, GearDisplayCatalog displayCatalog)
    {
        Unsubscribe();
        abilityState = state;
        catalog = displayCatalog;
        loggedMissingDefinitions.Clear();
        Rebuild();

        if (isShown)
        {
            Subscribe();
        }
    }

    public void Show()
    {
        if (contentRoot != null)
        {
            contentRoot.SetActive(true);
        }

        isShown = true;

        // Explicit full refresh on every open, then subscribe: reopening after a save/load is
        // state restoration, so the snapshot must be re-read rather than inferred from events.
        Rebuild();
        Subscribe();
    }

    public void Hide()
    {
        Unsubscribe();
        isShown = false;

        if (contentRoot != null)
        {
            contentRoot.SetActive(false);
        }
    }

    public bool HandleBackInternally()
    {
        // Package A2 Gear has no nested page, so Back falls through to the root close.
        return false;
    }

    public void SetTabStripReturnTarget(Selectable target)
    {
        stripReturnTarget = target;
        BuildEntryNavigation();
    }

    // -------------------------------------------------------------------------
    // Ownership subscription
    // -------------------------------------------------------------------------

    private void Subscribe()
    {
        if (subscribed || abilityState == null)
        {
            return;
        }

        abilityState.AbilityChanged += HandleAbilityChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || abilityState == null)
        {
            subscribed = false;
            return;
        }

        abilityState.AbilityChanged -= HandleAbilityChanged;
        subscribed = false;
    }

    private void HandleAbilityChanged(AbilityId ability, bool unlocked)
    {
        // Neutral refresh only. This is an ownership-changed signal, not an acquisition event, so
        // it must never produce a toast, sound, or "new!" treatment here.
        Rebuild();
    }

    // -------------------------------------------------------------------------
    // Presentation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the acquired-only entry list from the current ownership snapshot, restores the
    /// selected stable key when it is still owned, and rewires explicit list navigation.
    /// </summary>
    public void Rebuild()
    {
        ClearEntries();

        if (catalog != null && abilityState != null && entryContainer != null && entryPrefab != null)
        {
            IReadOnlyList<GearDisplayDefinition> definitions = catalog.Definitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                GearDisplayDefinition definition = definitions[i];
                if (definition == null || !abilityState.IsUnlocked(definition.RequiredAbility))
                {
                    continue;
                }

                GearEntryView entry = Instantiate(entryPrefab, entryContainer);
                entry.Bind(definition);
                entry.Activated += HandleEntryActivated;
                activeEntries.Add(entry);
            }
        }

        ReportUnlockedAbilitiesWithNoDefinition();

        bool hasEntries = activeEntries.Count > 0;
        if (emptyStateRoot != null) emptyStateRoot.SetActive(!hasEntries);
        if (populatedRoot != null) populatedRoot.SetActive(hasEntries);

        BuildEntryNavigation();
        RestoreSelectedDefinition();
    }

    private void ClearEntries()
    {
        for (int i = 0; i < activeEntries.Count; i++)
        {
            GearEntryView entry = activeEntries[i];
            if (entry == null)
            {
                continue;
            }

            entry.Activated -= HandleEntryActivated;

            if (Application.isPlaying)
            {
                Destroy(entry.gameObject);
            }
            else
            {
                DestroyImmediate(entry.gameObject);
            }
        }

        activeEntries.Clear();
    }

    /// <summary>
    /// Logs once per ability when the player owns something the approved catalogue cannot present.
    /// The entry is omitted rather than faked; this is a content gap, not a runtime failure.
    /// </summary>
    private void ReportUnlockedAbilitiesWithNoDefinition()
    {
        if (abilityState == null || catalog == null)
        {
            return;
        }

        foreach (AbilityId ability in System.Enum.GetValues(typeof(AbilityId)))
        {
            if (!abilityState.IsUnlocked(ability) || catalog.FindByAbility(ability) != null)
            {
                continue;
            }

            if (loggedMissingDefinitions.Add(ability))
            {
                Debug.Log(
                    $"[GearScreen] '{ability}' is unlocked but has no approved Gear display definition; " +
                    "it is omitted from Gear until a physical identity is authored.",
                    this);
            }
        }
    }

    private void BuildEntryNavigation()
    {
        for (int i = 0; i < activeEntries.Count; i++)
        {
            Selectable selectable = activeEntries[i]?.Selectable;
            if (selectable == null)
            {
                continue;
            }

            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = i > 0 ? activeEntries[i - 1].Selectable : stripReturnTarget;
            navigation.selectOnDown = i < activeEntries.Count - 1 ? activeEntries[i + 1].Selectable : null;
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            selectable.navigation = navigation;
        }
    }

    /// <summary>
    /// Keeps details on the previously selected Gear when it is still owned; otherwise falls back
    /// to the first visible entry, or clears details entirely in the empty state.
    /// </summary>
    private void RestoreSelectedDefinition()
    {
        if (activeEntries.Count == 0)
        {
            selectedStableKey = null;
            detailsPanel?.Clear();
            return;
        }

        GearEntryView match = null;
        if (!string.IsNullOrEmpty(selectedStableKey))
        {
            for (int i = 0; i < activeEntries.Count; i++)
            {
                if (activeEntries[i] != null && activeEntries[i].StableKey == selectedStableKey)
                {
                    match = activeEntries[i];
                    break;
                }
            }
        }

        match ??= activeEntries[0];
        ShowDetailsFor(match.StableKey);
    }

    private void HandleEntryActivated(string stableKey)
    {
        ShowDetailsFor(stableKey);
    }

    private void ShowDetailsFor(string stableKey)
    {
        selectedStableKey = stableKey;
        for (int i = 0; i < activeEntries.Count; i++)
        {
            GearEntryView entry = activeEntries[i];
            entry?.SetSelected(entry.StableKey == selectedStableKey);
        }

        detailsPanel?.Show(catalog != null ? catalog.FindByStableKey(stableKey) : null);
    }
}
