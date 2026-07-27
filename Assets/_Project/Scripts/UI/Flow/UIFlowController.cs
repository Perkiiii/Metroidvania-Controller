using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Which pausing root is active. Package A1 implements the Pause root fully; GameplayMenu is
/// routed and gated but has no registered production screen yet (see
/// Docs/FeatureSpecs/PauseAndMenuFlow.md, Package A2).
/// </summary>
public enum UIRootKind
{
    Pause,
    GameplayMenu
}

internal enum UIFlowState
{
    Closed,
    Opening,
    Open,
    Closing
}

/// <summary>
/// Persistent focused UI-flow coordinator. Owns root open/close requests, one active pausing
/// root, shallow modal/back routing, pause ownership tracking via <see cref="GameManager"/>,
/// System/UI input-map mode, scene-transition root availability + post-transition lockout, and
/// EventSystem first selection. It does not own HUD state, ability state, save data, scene
/// loading, map discovery, inventory, notifications, audio settings, or gameplay tuning — see
/// Docs/FeatureSpecs/UIArchitecture.md.
///
/// This is the single persistent owner of the "System" input action map (Pause, GameplayMenu).
/// The scene Hero never enables or disables it.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIFlowController : MonoBehaviour
{
    public static UIFlowController Instance { get; private set; }

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private InputActionReference pauseActionRef;
    [SerializeField] private InputActionReference gameplayMenuActionRef;
    [SerializeField] private InputActionReference uiCancelActionRef;

    [Header("UI Composition")]
    [SerializeField] private EventSystem eventSystem;
    [Tooltip("Must implement IUIFlowRootScreen. Production always authors this in Package A1.")]
    [SerializeField] private MonoBehaviour pauseRootBehaviour;
    [Tooltip("Must implement IUIFlowRootScreen. Left unassigned in Package A1 — GameplayMenu " +
        "requests are safely rejected (no pause, no blank screen, no selection change) until " +
        "Package A2 registers a real screen.")]
    [SerializeField] private MonoBehaviour gameplayMenuRootBehaviour;

    [Header("Transition Availability")]
    [Tooltip("Unscaled seconds both roots remain rejected after a scene transition completes.")]
    [SerializeField, Min(0f)] private float postTransitionLockoutSeconds = 1.0f;

    [Header("Health gate")]
    [Tooltip("Read-only consult only (death rejects root opens); UI never owns or mutates this.")]
    [SerializeField] private PlayerHealthState healthState;

    private IUIFlowRootScreen pauseRoot;
    private IUIFlowRootScreen gameplayMenuRoot;

    private InputAction pauseAction;
    private InputAction gameplayMenuAction;
    private InputAction uiCancelAction;
    private InputActionMap systemMap;
    private InputActionMap uiMap;

    private UIFlowState state = UIFlowState.Closed;
    private UIRootKind? activeKind;
    private bool uiOwnsPause;

    // Unavailable from boot until the first full scene transition has completed and its lockout
    // has elapsed — never a magic "armed at start" assumption.
    private bool wasTransitioning;
    private float lockoutRemaining = float.MaxValue;
    private bool isApplicationQuitting;

    public bool IsRootOpen => state == UIFlowState.Open;
    public UIRootKind? ActiveRootKind => activeKind;

    /// <summary>
    /// Assigns the root screens this controller drives. Production wires <see
    /// cref="pauseRootBehaviour"/>/<see cref="gameplayMenuRootBehaviour"/> via the Inspector;
    /// this is also the seam the UI Sandbox and tests use to supply lightweight/fake roots.
    /// </summary>
    public void ConfigureRoots(IUIFlowRootScreen pause, IUIFlowRootScreen gameplayMenu)
    {
        SetPauseRoot(pause);
        SetGameplayMenuRoot(gameplayMenu);
    }

    private void SetPauseRoot(IUIFlowRootScreen root)
    {
        if (pauseRoot != null) pauseRoot.CloseRequested -= HandlePauseRootCloseRequested;
        pauseRoot = root;
        if (pauseRoot != null) pauseRoot.CloseRequested += HandlePauseRootCloseRequested;
    }

    private void SetGameplayMenuRoot(IUIFlowRootScreen root)
    {
        if (gameplayMenuRoot != null) gameplayMenuRoot.CloseRequested -= HandleGameplayMenuRootCloseRequested;
        gameplayMenuRoot = root;
        if (gameplayMenuRoot != null) gameplayMenuRoot.CloseRequested += HandleGameplayMenuRootCloseRequested;
    }

    private void HandlePauseRootCloseRequested()
    {
        if (state == UIFlowState.Open && activeKind == UIRootKind.Pause)
        {
            RequestCloseActiveRoot();
        }
    }

    private void HandleGameplayMenuRootCloseRequested()
    {
        if (state == UIFlowState.Open && activeKind == UIRootKind.GameplayMenu)
        {
            RequestCloseActiveRoot();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveRoots();
        ResolveActions();
        EnableSystemMap();
        SetUiNavigationEnabled(false);
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        // Only the legitimate active singleton reaches here (duplicates self-destroy in Awake
        // before ever becoming Instance). Release an owned pause through the same close sequence
        // used by a normal close so gameplay is never left paused/input-suspended behind a
        // destroyed controller. Skipped during application shutdown, where GameManager/Hero may
        // already be tearing down and resuming input would be meaningless.
        if (!isApplicationQuitting && state == UIFlowState.Open)
        {
            RequestCloseActiveRoot();
        }

        DisableSystemMap();
        SetPauseRoot(null);
        SetGameplayMenuRoot(null);
        Instance = null;
    }

    private void Update()
    {
        UpdateTransitionLockout();

        if (pauseAction != null && pauseAction.WasPressedThisFrame())
        {
            HandlePausePressed();
        }

        if (gameplayMenuAction != null && gameplayMenuAction.WasPressedThisFrame())
        {
            HandleGameplayMenuPressed();
        }

        if (state == UIFlowState.Open && uiCancelAction != null && uiCancelAction.WasPressedThisFrame())
        {
            HandleCancelPressed();
        }
    }

    private void ResolveRoots()
    {
        if (pauseRootBehaviour is IUIFlowRootScreen pause)
        {
            SetPauseRoot(pause);
        }

        if (gameplayMenuRootBehaviour is IUIFlowRootScreen gameplayMenu)
        {
            SetGameplayMenuRoot(gameplayMenu);
        }
    }

    private void ResolveActions()
    {
        systemMap = inputActions != null ? inputActions.FindActionMap("System", false) : null;
        uiMap = inputActions != null ? inputActions.FindActionMap("UI", false) : null;

        pauseAction = ResolveAction(pauseActionRef, systemMap, "Pause");
        gameplayMenuAction = ResolveAction(gameplayMenuActionRef, systemMap, "GameplayMenu");
        uiCancelAction = ResolveAction(uiCancelActionRef, uiMap, "Cancel");
    }

    private static InputAction ResolveAction(InputActionReference reference, InputActionMap fallbackMap, string actionName)
    {
        if (reference != null && reference.action != null)
        {
            return reference.action;
        }

        return fallbackMap != null ? fallbackMap.FindAction(actionName, false) : null;
    }

    private void EnableSystemMap()
    {
        systemMap?.Enable();
    }

    private void DisableSystemMap()
    {
        systemMap?.Disable();
    }

    private void SetUiNavigationEnabled(bool enabled)
    {
        if (uiMap == null)
        {
            return;
        }

        if (enabled)
        {
            uiMap.Enable();
        }
        else
        {
            uiMap.Disable();
        }
    }

    // -------------------------------------------------------------------------
    // Root availability
    // -------------------------------------------------------------------------

    private void UpdateTransitionLockout()
    {
        GameManager gm = GameManager.Instance;
        bool isTransitioning = gm != null && gm.IsSceneTransitioning;

        if (wasTransitioning && !isTransitioning && gm != null && gm.State == GameState.Playing)
        {
            lockoutRemaining = postTransitionLockoutSeconds;
        }

        wasTransitioning = isTransitioning;

        if (lockoutRemaining > 0f && lockoutRemaining < float.MaxValue)
        {
            lockoutRemaining = Mathf.Max(0f, lockoutRemaining - Time.unscaledDeltaTime);
        }
    }

    private bool CanAcceptNewRootRequest()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return false;
        if (gm.State != GameState.Playing) return false;
        if (gm.IsSceneTransitioning) return false;
        if (lockoutRemaining > 0f) return false;
        if (gm.IsRespawnOrRecoveryInProgress) return false;
        if (healthState != null && healthState.IsDepleted) return false;
        if (state != UIFlowState.Closed) return false;
        return true;
    }

    // -------------------------------------------------------------------------
    // Request handling (edge-triggered: WasPressedThisFrame is inherently per-action and only
    // fires once per physical press, so holding a control through a blocked window can never by
    // itself open a root later, and releasing one root action never affects the other's arming).
    // -------------------------------------------------------------------------

    private void HandlePausePressed()
    {
        if (state == UIFlowState.Open && activeKind == UIRootKind.Pause)
        {
            // A modal (e.g. Quit confirmation) takes Pause first: close only the modal through
            // the same path Back/Cancel uses, keep the root open, keep gameplay paused, and skip
            // the Hero input-resume/Unpause sequence entirely. A later Pause press at the bare
            // root falls through to the normal close below.
            if (pauseRoot != null && pauseRoot.HasOpenModal)
            {
                pauseRoot.HandleBackInternally();
                return;
            }

            RequestCloseActiveRoot();
            return;
        }

        if (!CanAcceptNewRootRequest())
        {
            return;
        }

        if (pauseRoot == null)
        {
            return;
        }

        OpenRoot(UIRootKind.Pause, pauseRoot);
    }

    private void HandleGameplayMenuPressed()
    {
        if (state == UIFlowState.Open && activeKind == UIRootKind.GameplayMenu)
        {
            RequestCloseActiveRoot();
            return;
        }

        if (!CanAcceptNewRootRequest())
        {
            return;
        }

        if (gameplayMenuRoot == null)
        {
            // No production Gameplay Menu screen registered in Package A1. Reject safely: do not
            // pause, do not display anything, do not touch selection.
            return;
        }

        OpenRoot(UIRootKind.GameplayMenu, gameplayMenuRoot);
    }

    private void HandleCancelPressed()
    {
        if (state != UIFlowState.Open || activeKind == null)
        {
            return;
        }

        IUIFlowRootScreen root = activeKind == UIRootKind.Pause ? pauseRoot : gameplayMenuRoot;
        if (root == null)
        {
            return;
        }

        if (root.HandleBackInternally())
        {
            return;
        }

        RequestCloseActiveRoot();
    }

    // -------------------------------------------------------------------------
    // Open / close sequences
    // -------------------------------------------------------------------------

    private void OpenRoot(UIRootKind kind, IUIFlowRootScreen screen)
    {
        state = UIFlowState.Opening;
        activeKind = kind;

        HeroController hero = ResolveCurrentHero();
        hero?.SuspendGameplayInput();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.Pause();
            uiOwnsPause = true;
        }

        SetUiNavigationEnabled(true);
        screen.Show();
        UISelectionUtility.Select(eventSystem, screen.FirstSelection);

        state = UIFlowState.Open;
    }

    private void RequestCloseActiveRoot()
    {
        if (state != UIFlowState.Open || activeKind == null)
        {
            return;
        }

        state = UIFlowState.Closing;

        IUIFlowRootScreen screen = activeKind == UIRootKind.Pause ? pauseRoot : gameplayMenuRoot;
        screen?.Hide();

        if (eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(null);
        }

        HeroController hero = ResolveCurrentHero();
        // Keep gameplay paused and command sampling suspended through this point; only clear
        // transient buffers/snapshots here. Overlapping close controls (e.g. UI Cancel / Gamepad
        // East, which also drives Bind) are handled by BeginGameplayInputResume's per-command
        // held-at-resume disarm below, not by an artificial wait for physical release.
        hero?.ClearTransientGameplayInput();

        SetUiNavigationEnabled(false);
        hero?.BeginGameplayInputResume();

        if (uiOwnsPause && GameManager.Instance != null)
        {
            GameManager.Instance.Unpause();
        }
        uiOwnsPause = false;

        activeKind = null;
        state = UIFlowState.Closed;
    }

    /// <summary>
    /// Resolves the current scene Hero just-in-time through the established
    /// <see cref="GameManager"/> seam rather than caching a reference that could go stale across
    /// a scene load.
    /// </summary>
    private static HeroController ResolveCurrentHero()
    {
        return GameManager.Instance != null ? GameManager.Instance.CurrentHero : null;
    }
}
