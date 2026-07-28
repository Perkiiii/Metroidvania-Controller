using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The persistent, pausing Gameplay Menu root: the second <see cref="IUIFlowRootScreen"/> after
/// <see cref="PauseMenuScreen"/>. See Docs/FeatureSpecs/GameplayMenu.md.
///
/// Ownership boundary. This screen owns exactly: which of the seven fixed tabs is active, the
/// runtime-only last-tab and per-tab selection memory, the tab strip's presentation and explicit
/// navigation, Previous/Next tab cycling, and Back delegation into the active tab.
/// <see cref="UIFlowController"/> keeps sole ownership of root availability,
/// <see cref="GameManager"/> pause, input-map mode, Hero input suspension/resume, and root-level
/// selection entry. This screen never writes <c>Time.timeScale</c>, never touches
/// <c>HeroInputReader</c>, never loads a scene, never saves, and owns no gameplay state.
///
/// Tab memory is runtime-only: it is not serialized to disk, not static, not in PlayerPrefs, and
/// not in SaveData. It survives room transitions purely because this screen is persistent.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayMenuScreen : MonoBehaviour, IUIFlowRootScreen
{
    /// <summary>Authored tab order. Registrations must match this exactly.</summary>
    public static readonly GameplayMenuTabId[] FixedTabOrder =
    {
        GameplayMenuTabId.Gear,
        GameplayMenuTabId.Tools,
        GameplayMenuTabId.Satchel,
        GameplayMenuTabId.Recipes,
        GameplayMenuTabId.Tasks,
        GameplayMenuTabId.Journal,
        GameplayMenuTabId.Map
    };

    public const GameplayMenuTabId DefaultTab = GameplayMenuTabId.Gear;

    [Header("Composition")]
    [Tooltip("Full-screen presentation root. Must default to inactive so a closed menu never " +
        "blocks HUD raycasts.")]
    [SerializeField] private GameObject visualRoot;

    [Tooltip("Same EventSystem the persistent MenuRoot (or Sandbox-local) input module drives.")]
    [SerializeField] private EventSystem eventSystem;

    [Tooltip("Always-visible global Close control. Also the last-resort selection fallback.")]
    [SerializeField] private Button closeButton;

    [Tooltip("Shows the active tab's player-facing name below the strip. This is what keeps the " +
        "active tab named at narrow aspect ratios, where the per-tab titles collapse to glyphs.")]
    [SerializeField] private Text activeTabTitleLabel;

    [Header("Top tab strip")]
    [Tooltip("Viewport the horizontal strip is laid out inside. Its width decides whether the " +
        "strip presents glyph + title or collapses to glyph-only. Never used to hide a tab.")]
    [SerializeField] private RectTransform stripViewport;

    [Tooltip("Horizontal spacing between strip cells, matching the strip's HorizontalLayoutGroup.")]
    [SerializeField, Min(0f)] private float stripCellSpacing = 8f;

    [Tooltip("Extra width the Previous/Next hints and frame padding need beside the strip before " +
        "the cells must collapse.")]
    [SerializeField, Min(0f)] private float stripReservedWidth = 240f;

    [Header("Tabs — exactly seven, in fixed order")]
    [SerializeField] private List<GameplayMenuTabRegistration> tabs = new List<GameplayMenuTabRegistration>();

    [Header("Tab cycling input (UI map)")]
    [Tooltip("Fallback source for UI/PreviousTab and UI/NextTab when the durable references below " +
        "are unassigned. Mirrors UIFlowController's resolve-by-map/name fallback.")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private InputActionReference previousTabActionRef;
    [SerializeField] private InputActionReference nextTabActionRef;

    [Tooltip("Provisional: Previous from Gear wraps to Map and Next from Map wraps to Gear.")]
    [SerializeField] private bool wrapTabCycling = true;

    /// <summary>Fired when the global Close control is activated. Never unpauses directly.</summary>
    public event Action CloseRequested;

    private readonly Dictionary<GameplayMenuTabId, Selectable> rememberedSelections =
        new Dictionary<GameplayMenuTabId, Selectable>();
    private readonly List<Action> tabButtonHandlers = new List<Action>();

    private GameplayMenuTabId activeTabId = DefaultTab;
    private GameplayMenuTabId lastValidTab = DefaultTab;
    private bool initialized;
    private bool isShown;

    private InputAction previousTabAction;
    private InputAction nextTabAction;
    private bool previousTabEnabledByScreen;
    private bool nextTabEnabledByScreen;

    /// <summary>The tab currently presented. Retains its last value while hidden.</summary>
    public GameplayMenuTabId ActiveTabId => activeTabId;

    /// <summary>True between <see cref="Show"/> and <see cref="Hide"/>.</summary>
    public bool IsShown => isShown;

    public bool HasOpenModal
    {
        get
        {
            if (!isShown) return false;
            GameplayMenuTabRegistration registration = FindRegistration(activeTabId);
            return registration?.View != null && registration.View.HasOpenModal;
        }
    }

    /// <summary>
    /// Selection entry point used by <see cref="UIFlowController"/> immediately after
    /// <see cref="Show"/>. Resolution order: remembered valid selection for the active tab, the
    /// tab's own first selection, the registration's authored fallback, the active tab button,
    /// then the global Close control. Never returns an inactive/non-interactable control.
    /// </summary>
    public Selectable FirstSelection => ResolveSelectionForTab(activeTabId, preferStripButton: false);

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        UnsubscribeTabActions();
        UnsubscribeTabButtons();
    }

    private void LateUpdate()
    {
        // Keep the strip readable if the window or aspect preview changes while the menu is open.
        // Presentation only, and only while shown; it changes no state and selects nothing.
        if (isShown)
        {
            RefreshStripDensity();
        }
    }

    /// <summary>
    /// Runtime composition seam. Production authors everything in the Inspector; EditMode tests use
    /// this to supply a lightweight seven-tab fixture, mirroring
    /// <see cref="UIFlowController.ConfigureRoots"/>.
    /// </summary>
    public void Configure(
        EventSystem eventSystemToUse,
        GameObject visualRootToUse,
        Button closeButtonToUse,
        IEnumerable<GameplayMenuTabRegistration> registrations)
    {
        UnsubscribeTabActions();
        UnsubscribeTabButtons();

        eventSystem = eventSystemToUse;
        visualRoot = visualRootToUse;
        closeButton = closeButtonToUse;

        tabs.Clear();
        if (registrations != null)
        {
            tabs.AddRange(registrations);
        }

        initialized = false;
        isShown = false;
        rememberedSelections.Clear();
        activeTabId = DefaultTab;
        lastValidTab = DefaultTab;
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        ValidateRegistrations();
        ResolveTabActions();
        SubscribeTabButtons();
        BuildStripNavigation();

        // Fail closed: every tab starts hidden and the whole root starts inactive, so a closed
        // menu can never block HUD raycasts or leave two tabs visible at once.
        for (int i = 0; i < tabs.Count; i++)
        {
            tabs[i]?.View?.Hide();
            tabs[i]?.TabButton?.SetActiveTab(false);
            tabs[i]?.TabButton?.SetLabel(tabs[i].DisplayName);
        }

        if (visualRoot != null)
        {
            visualRoot.SetActive(false);
        }

        if (!HasRegistration(activeTabId))
        {
            activeTabId = ResolveAnyAvailableTab();
            lastValidTab = activeTabId;
        }
    }

    private void ValidateRegistrations()
    {
        if (tabs.Count != FixedTabOrder.Length)
        {
            Debug.LogError(
                $"[GameplayMenuScreen] '{name}' has {tabs.Count} tab registration(s); exactly " +
                $"{FixedTabOrder.Length} are required (Gear, Tools, Satchel, Recipes, Tasks, Journal, Map).",
                this);
        }

        HashSet<GameplayMenuTabId> seen = new HashSet<GameplayMenuTabId>();
        for (int i = 0; i < tabs.Count; i++)
        {
            GameplayMenuTabRegistration registration = tabs[i];
            if (registration == null)
            {
                Debug.LogError($"[GameplayMenuScreen] '{name}' tab registration {i} is null.", this);
                continue;
            }

            if (!seen.Add(registration.Id))
            {
                Debug.LogError($"[GameplayMenuScreen] '{name}' registers tab '{registration.Id}' more than once.", this);
            }

            if (i < FixedTabOrder.Length && registration.Id != FixedTabOrder[i])
            {
                Debug.LogError(
                    $"[GameplayMenuScreen] '{name}' tab registration {i} is '{registration.Id}'; " +
                    $"the fixed order requires '{FixedTabOrder[i]}'.",
                    this);
            }

            if (registration.TabViewBehaviour == null)
            {
                Debug.LogError($"[GameplayMenuScreen] '{name}' tab '{registration.Id}' has no tab view assigned.", this);
            }
            else if (registration.View == null)
            {
                Debug.LogError(
                    $"[GameplayMenuScreen] '{name}' tab '{registration.Id}' view " +
                    $"'{registration.TabViewBehaviour.GetType().Name}' does not implement IGameplayMenuTab.",
                    this);
            }

            if (registration.TabButton == null)
            {
                Debug.LogError($"[GameplayMenuScreen] '{name}' tab '{registration.Id}' has no tab button assigned.", this);
            }
        }

        if (closeButton == null)
        {
            Debug.LogError($"[GameplayMenuScreen] '{name}' has no global Close control assigned.", this);
        }
    }

    // -------------------------------------------------------------------------
    // IUIFlowRootScreen
    // -------------------------------------------------------------------------

    public void Show()
    {
        EnsureInitialized();

        GameplayMenuTabId target = HasRegistration(lastValidTab) ? lastValidTab : ResolveAnyAvailableTab();

        if (visualRoot != null)
        {
            visualRoot.SetActive(true);
        }

        isShown = true;
        SubscribeTabActions();

        // UIFlowController applies FirstSelection immediately after Show(), so this must not
        // establish selection itself (doing so would double-select and fight root-level entry).
        ApplyActiveTab(target, SelectionPolicy.None);
    }

    public void Hide()
    {
        // Idempotent: a second Hide (e.g. teardown after a toggle-close) must be harmless.
        if (isShown)
        {
            CaptureCurrentSelection();
        }

        UnsubscribeTabActions();

        for (int i = 0; i < tabs.Count; i++)
        {
            tabs[i]?.View?.Hide();
        }

        if (visualRoot != null)
        {
            visualRoot.SetActive(false);
        }

        isShown = false;
    }

    public bool HandleBackInternally()
    {
        if (!isShown)
        {
            return false;
        }

        GameplayMenuTabRegistration registration = FindRegistration(activeTabId);
        return registration?.View != null && registration.View.HandleBackInternally();
    }

    /// <summary>
    /// Raised by the authored global Close control. Requests a full root close through
    /// <see cref="UIFlowController"/>'s normal sequence; this screen never unpauses directly.
    /// </summary>
    public void RequestClose()
    {
        CloseRequested?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Tab selection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Switches to <paramref name="tabId"/>. Returns false for an unregistered tab, while hidden,
    /// or while the active tab owns a nested layer that must close first.
    /// </summary>
    public bool SelectTab(GameplayMenuTabId tabId)
    {
        EnsureInitialized();

        if (!HasRegistration(tabId))
        {
            Debug.LogError($"[GameplayMenuScreen] '{name}' cannot select unregistered tab '{tabId}'.", this);
            return false;
        }

        if (!isShown || HasOpenModal)
        {
            return false;
        }

        if (tabId == activeTabId)
        {
            return true;
        }

        bool focusWasOnStrip = CurrentSelectionIsStripControl();
        CaptureCurrentSelection();
        ApplyActiveTab(tabId, focusWasOnStrip ? SelectionPolicy.KeepOnStrip : SelectionPolicy.EnterContent);
        return true;
    }

    public bool SelectPreviousTab() => CycleTab(-1);

    public bool SelectNextTab() => CycleTab(1);

    private bool CycleTab(int direction)
    {
        if (!isShown || HasOpenModal)
        {
            return false;
        }

        int index = IndexOf(activeTabId);
        if (index < 0)
        {
            return false;
        }

        int target = index + direction;
        if (target < 0 || target >= tabs.Count)
        {
            if (!wrapTabCycling)
            {
                return false;
            }

            target = (target + tabs.Count) % tabs.Count;
        }

        GameplayMenuTabRegistration registration = tabs[target];
        return registration != null && SelectTab(registration.Id);
    }

    private enum SelectionPolicy
    {
        /// <summary>Leave selection alone — the caller establishes it (root open).</summary>
        None,

        /// <summary>Focus was in the tab strip: keep it there, on the newly active tab button.</summary>
        KeepOnStrip,

        /// <summary>Focus was in content: move to the new tab's remembered/first selection.</summary>
        EnterContent
    }

    private void ApplyActiveTab(GameplayMenuTabId tabId, SelectionPolicy selectionPolicy)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            GameplayMenuTabRegistration registration = tabs[i];
            if (registration == null)
            {
                continue;
            }

            bool isActive = registration.Id == tabId;
            registration.TabButton?.SetActiveTab(isActive);

            if (!isActive)
            {
                registration.View?.Hide();
            }
        }

        activeTabId = tabId;
        lastValidTab = tabId;

        GameplayMenuTabRegistration active = FindRegistration(tabId);
        active?.View?.Show();

        if (activeTabTitleLabel != null && active != null)
        {
            activeTabTitleLabel.text = active.DisplayName;
        }

        WireActiveTabNavigation(active);
        RefreshStripDensity();

        switch (selectionPolicy)
        {
            case SelectionPolicy.KeepOnStrip:
                UISelectionUtility.Select(eventSystem, ResolveSelectionForTab(tabId, preferStripButton: true));
                break;
            case SelectionPolicy.EnterContent:
                UISelectionUtility.Select(eventSystem, ResolveSelectionForTab(tabId, preferStripButton: false));
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Selection resolution and memory
    // -------------------------------------------------------------------------

    private Selectable ResolveSelectionForTab(GameplayMenuTabId tabId, bool preferStripButton)
    {
        GameplayMenuTabRegistration registration = FindRegistration(tabId);
        Selectable tabButton = registration?.TabButton != null ? registration.TabButton.Selectable : null;

        if (preferStripButton && UISelectionUtility.IsSelectable(tabButton))
        {
            return tabButton;
        }

        if (rememberedSelections.TryGetValue(tabId, out Selectable remembered) && UISelectionUtility.IsSelectable(remembered))
        {
            return remembered;
        }

        Selectable viewFirst = registration?.View?.FirstSelection;
        if (UISelectionUtility.IsSelectable(viewFirst))
        {
            return viewFirst;
        }

        if (UISelectionUtility.IsSelectable(registration?.FirstSelectionFallback))
        {
            return registration.FirstSelectionFallback;
        }

        if (UISelectionUtility.IsSelectable(tabButton))
        {
            return tabButton;
        }

        return UISelectionUtility.IsSelectable(closeButton) ? closeButton : null;
    }

    /// <summary>
    /// Remembers the current selection for the active tab, but only when it genuinely belongs to
    /// that tab's authored content. Strip buttons and the global Close control are shared frame
    /// chrome and are never stored as a tab's content memory.
    /// </summary>
    private void CaptureCurrentSelection()
    {
        if (eventSystem == null)
        {
            return;
        }

        GameObject current = eventSystem.currentSelectedGameObject;
        if (current == null)
        {
            return;
        }

        GameplayMenuTabRegistration registration = FindRegistration(activeTabId);
        Transform viewTransform = registration?.ViewTransform;
        if (viewTransform == null || !current.transform.IsChildOf(viewTransform))
        {
            return;
        }

        Selectable selectable = current.GetComponent<Selectable>();
        if (selectable != null)
        {
            rememberedSelections[activeTabId] = selectable;
        }
    }

    private bool CurrentSelectionIsStripControl()
    {
        if (eventSystem == null)
        {
            return false;
        }

        GameObject current = eventSystem.currentSelectedGameObject;
        if (current == null)
        {
            return false;
        }

        for (int i = 0; i < tabs.Count; i++)
        {
            GameplayMenuTabButton button = tabs[i]?.TabButton;
            if (button != null && button.gameObject == current)
            {
                return true;
            }
        }

        return closeButton != null && closeButton.gameObject == current;
    }

    // -------------------------------------------------------------------------
    // Explicit navigation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds the horizontal top-strip chain in code so it can never drift from the authored tab
    /// order: Left/Right walk the seven tabs and wrap at both ends, and Up leaves the strip for the
    /// global Close control. Down (into the active tab's content) and Close's own Down are re-wired
    /// per tab by <see cref="WireActiveTabNavigation"/>, because both depend on which tab is active.
    /// </summary>
    private void BuildStripNavigation()
    {
        List<Selectable> strip = new List<Selectable>();
        for (int i = 0; i < tabs.Count; i++)
        {
            Selectable tabButton = tabs[i]?.TabButton != null ? tabs[i].TabButton.Selectable : null;
            if (tabButton != null)
            {
                strip.Add(tabButton);
            }
        }

        for (int i = 0; i < strip.Count; i++)
        {
            Navigation navigation = strip[i].navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnLeft = strip[(i - 1 + strip.Count) % strip.Count];
            navigation.selectOnRight = strip[(i + 1) % strip.Count];
            navigation.selectOnUp = closeButton;
            navigation.selectOnDown = null;
            strip[i].navigation = navigation;
        }

        if (closeButton != null)
        {
            Navigation closeNavigation = closeButton.navigation;
            closeNavigation.mode = Navigation.Mode.Explicit;
            closeNavigation.selectOnLeft = null;
            closeNavigation.selectOnRight = null;
            closeNavigation.selectOnUp = null;
            closeNavigation.selectOnDown = strip.Count > 0 ? strip[0] : null;
            closeButton.navigation = closeNavigation;
        }
    }

    /// <summary>
    /// Points the active tab button's Down at that tab's content entry point, and Close's Down back
    /// at the active tab so leaving and re-entering the strip lands where the player was. Empty-state
    /// tabs expose no content, so Down simply does nothing there rather than jumping into another
    /// tab's content.
    /// </summary>
    private void WireActiveTabNavigation(GameplayMenuTabRegistration registration)
    {
        if (registration?.TabButton == null)
        {
            return;
        }

        Selectable tabButton = registration.TabButton.Selectable;
        if (tabButton == null)
        {
            return;
        }

        if (registration.View is IGameplayMenuTabNavigation tabNavigation)
        {
            tabNavigation.SetTabStripReturnTarget(tabButton);
        }

        Selectable contentEntry = registration.View?.FirstSelection;
        if (!UISelectionUtility.IsSelectable(contentEntry))
        {
            contentEntry = UISelectionUtility.IsSelectable(registration.FirstSelectionFallback)
                ? registration.FirstSelectionFallback
                : null;
        }

        Navigation navigation = tabButton.navigation;
        navigation.selectOnDown = contentEntry;
        tabButton.navigation = navigation;

        if (closeButton != null)
        {
            Navigation closeNavigation = closeButton.navigation;
            closeNavigation.selectOnDown = tabButton;
            closeButton.navigation = closeNavigation;
        }
    }

    // -------------------------------------------------------------------------
    // Narrow-strip presentation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Collapses every strip cell to glyph-only when the viewport cannot fit glyph + title for all
    /// seven, and expands them again when it can. Tabs are never hidden, disabled, scrolled out of
    /// reach, or dropped from the cycle — only the per-tab titles collapse, and
    /// <see cref="activeTabTitleLabel"/> keeps naming the active tab. A null viewport (EditMode
    /// fixtures) simply stays expanded.
    /// </summary>
    private void RefreshStripDensity()
    {
        if (stripViewport == null || tabs.Count == 0)
        {
            return;
        }

        float required = stripReservedWidth + stripCellSpacing * Mathf.Max(0, tabs.Count - 1);
        for (int i = 0; i < tabs.Count; i++)
        {
            GameplayMenuTabButton button = tabs[i]?.TabButton;
            required += button != null ? button.ExpandedWidth : 0f;
        }

        bool compact = stripViewport.rect.width < required;
        for (int i = 0; i < tabs.Count; i++)
        {
            tabs[i]?.TabButton?.SetCompact(compact);
        }
    }

    /// <summary>Editor/test accessor: true while the strip is presenting glyph-only cells.</summary>
    public bool IsStripCompact
    {
        get
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                GameplayMenuTabButton button = tabs[i]?.TabButton;
                if (button != null)
                {
                    return button.IsCompact;
                }
            }

            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Input
    // -------------------------------------------------------------------------

    private void ResolveTabActions()
    {
        InputActionMap uiMap = inputActions != null ? inputActions.FindActionMap("UI", false) : null;
        previousTabAction = ResolveAction(previousTabActionRef, uiMap, "PreviousTab");
        nextTabAction = ResolveAction(nextTabActionRef, uiMap, "NextTab");
    }

    private static InputAction ResolveAction(InputActionReference reference, InputActionMap fallbackMap, string actionName)
    {
        if (reference != null && reference.action != null)
        {
            return reference.action;
        }

        return fallbackMap != null ? fallbackMap.FindAction(actionName, false) : null;
    }

    /// <summary>
    /// Subscribes tab cycling only while the root is shown. In production the UI map is already
    /// enabled by <see cref="UIFlowController"/>, so nothing is enabled here; the narrow
    /// per-action enable exists only for direct-preview contexts (UI Sandbox) that have no flow
    /// controller. This screen never enables or disables the UI map itself.
    /// </summary>
    private void SubscribeTabActions()
    {
        if (previousTabAction != null)
        {
            previousTabAction.performed += HandlePreviousTabPerformed;
            if (!previousTabAction.enabled)
            {
                previousTabAction.Enable();
                previousTabEnabledByScreen = true;
            }
        }

        if (nextTabAction != null)
        {
            nextTabAction.performed += HandleNextTabPerformed;
            if (!nextTabAction.enabled)
            {
                nextTabAction.Enable();
                nextTabEnabledByScreen = true;
            }
        }
    }

    private void UnsubscribeTabActions()
    {
        if (previousTabAction != null)
        {
            previousTabAction.performed -= HandlePreviousTabPerformed;
            if (previousTabEnabledByScreen)
            {
                previousTabAction.Disable();
                previousTabEnabledByScreen = false;
            }
        }

        if (nextTabAction != null)
        {
            nextTabAction.performed -= HandleNextTabPerformed;
            if (nextTabEnabledByScreen)
            {
                nextTabAction.Disable();
                nextTabEnabledByScreen = false;
            }
        }
    }

    private void HandlePreviousTabPerformed(InputAction.CallbackContext _) => SelectPreviousTab();

    private void HandleNextTabPerformed(InputAction.CallbackContext _) => SelectNextTab();

    // Subscribed once at initialization (never per Show) so repeated open/close cycles cannot
    // accumulate duplicate click handlers.
    private void SubscribeTabButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(RequestClose);
        }

        for (int i = 0; i < tabs.Count; i++)
        {
            GameplayMenuTabRegistration registration = tabs[i];
            if (registration?.TabButton == null)
            {
                tabButtonHandlers.Add(null);
                continue;
            }

            GameplayMenuTabId capturedId = registration.Id;
            Action handler = () => SelectTab(capturedId);
            registration.TabButton.Clicked += handler;
            tabButtonHandlers.Add(handler);
        }
    }

    private void UnsubscribeTabButtons()
    {
        for (int i = 0; i < tabButtonHandlers.Count && i < tabs.Count; i++)
        {
            Action handler = tabButtonHandlers[i];
            GameplayMenuTabButton button = tabs[i]?.TabButton;
            if (handler != null && button != null)
            {
                button.Clicked -= handler;
            }
        }

        tabButtonHandlers.Clear();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(RequestClose);
        }
    }

    // -------------------------------------------------------------------------
    // Registration lookup
    // -------------------------------------------------------------------------

    private GameplayMenuTabRegistration FindRegistration(GameplayMenuTabId tabId)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] != null && tabs[i].Id == tabId)
            {
                return tabs[i];
            }
        }

        return null;
    }

    private bool HasRegistration(GameplayMenuTabId tabId) => FindRegistration(tabId) != null;

    private int IndexOf(GameplayMenuTabId tabId)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] != null && tabs[i].Id == tabId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Fail-closed fallback for a misauthored screen: prefer Gear, otherwise the first valid
    /// registration, so the root still opens with a usable selection instead of a blank frame.
    /// </summary>
    private GameplayMenuTabId ResolveAnyAvailableTab()
    {
        if (HasRegistration(DefaultTab))
        {
            return DefaultTab;
        }

        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] != null)
            {
                return tabs[i].Id;
            }
        }

        return DefaultTab;
    }
}
