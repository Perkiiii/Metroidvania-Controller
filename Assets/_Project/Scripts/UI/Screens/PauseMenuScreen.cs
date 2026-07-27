using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Root Pause menu: Continue, Options, Quit to Main Menu. See
/// Docs/FeatureSpecs/PauseAndMenuFlow.md. Continue and the generic close sequence are owned by
/// <see cref="UIFlowController"/>; this screen owns only its own presentation, the Options
/// production gate, and the Quit confirmation modal.
/// </summary>
public sealed class PauseMenuScreen : MonoBehaviour, IUIFlowRootScreen
{
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private ConfirmationModal quitConfirmationModal;
    [Tooltip("Same EventSystem the persistent MenuRoot (or Sandbox-local) input module drives. " +
        "Used only to restore a valid selection when a gated control is disabled while selected " +
        "and to let the Sandbox restore focus after closing its Options preview child.")]
    [SerializeField] private EventSystem eventSystem;

    [Header("Package A1 gating")]
    [Tooltip("Production Options remains non-functional until Package B. The Sandbox may enable " +
        "this to preview a labelled placeholder child flow.")]
    [SerializeField] private bool optionsPreviewEnabled;
    [Tooltip("Confirmed Quit is development-gated by default so production cannot present a fake " +
        "functional endpoint. The Sandbox may enable this to exercise the typed request seam.")]
    [SerializeField] private bool quitRequestSeamEnabled;

    /// <summary>Fired when Continue is clicked or Back/Cancel is pressed at the bare root.</summary>
    public event Action CloseRequested;

    /// <summary>
    /// Typed seam for a future functional Quit-to-Main-Menu flow. Package A1 never subscribes to
    /// this in production and never loads a scene from it.
    /// </summary>
    public event Action QuitToMainMenuRequested;

    /// <summary>
    /// Sandbox-only seam: fired when Options is clicked while <see cref="optionsPreviewEnabled"/>
    /// is true. Production never subscribes to this; PauseMenuScreen creates no settings data and
    /// owns no audio/display/accessibility/persistence state.
    /// </summary>
    public event Action OptionsRequested;

    public Selectable FirstSelection => continueButton;
    public bool HasOpenModal => quitConfirmationModal != null && quitConfirmationModal.IsOpen;

    private void Awake()
    {
        if (continueButton != null) continueButton.onClick.AddListener(HandleContinueClicked);
        if (optionsButton != null) optionsButton.onClick.AddListener(HandleOptionsClicked);
        if (quitButton != null) quitButton.onClick.AddListener(HandleQuitClicked);

        RefreshAvailabilityAndNavigation();
    }

    private void OnDestroy()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(HandleContinueClicked);
        if (optionsButton != null) optionsButton.onClick.RemoveListener(HandleOptionsClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(HandleQuitClicked);
    }

    public void Show()
    {
        if (visualRoot != null) visualRoot.SetActive(true);
    }

    public void Hide()
    {
        // Modal closes before the root per the approved Back/close hierarchy; if the root is
        // being hidden while a modal is still open (e.g. Pause toggled closed unexpectedly),
        // close it without restoring selection since the whole root is going away.
        if (HasOpenModal)
        {
            quitConfirmationModal.Close(restoreInvokerSelection: false);
        }

        if (visualRoot != null) visualRoot.SetActive(false);
    }

    public bool HandleBackInternally()
    {
        if (HasOpenModal)
        {
            quitConfirmationModal.HandleBack();
            return true;
        }

        return false;
    }

    /// <summary>Sandbox-only: preview the placeholder Options child flow and Back/focus behavior.</summary>
    public void SetOptionsPreviewEnabled(bool enabled)
    {
        optionsPreviewEnabled = enabled;
        RefreshAvailabilityAndNavigation();
    }

    /// <summary>Sandbox-only: enable the typed Quit request seam for verification.</summary>
    public void SetQuitRequestSeamEnabled(bool enabled)
    {
        quitRequestSeamEnabled = enabled;
        RefreshAvailabilityAndNavigation();
    }

    /// <summary>
    /// Sandbox-only seam: restores selection to the Options button after the Sandbox-only Options
    /// preview child closes. Falls back to Continue if Options is not currently interactable.
    /// </summary>
    public void RestoreOptionsSelection()
    {
        UISelectionUtility.SelectPreferredOrFallback(eventSystem, optionsButton, continueButton);
    }

    /// <summary>
    /// Rebuilds Continue/Options/Quit interactability and explicit keyboard/controller navigation
    /// so disabled controls are skipped entirely rather than merely un-clickable dead ends, and
    /// moves selection off a control that just became disabled while selected.
    /// </summary>
    private void RefreshAvailabilityAndNavigation()
    {
        if (optionsButton != null) optionsButton.interactable = optionsPreviewEnabled;
        if (quitButton != null) quitButton.interactable = quitRequestSeamEnabled;

        List<Button> active = new List<Button>();
        if (continueButton != null) active.Add(continueButton);
        if (optionsButton != null && optionsPreviewEnabled) active.Add(optionsButton);
        if (quitButton != null && quitRequestSeamEnabled) active.Add(quitButton);

        Button[] allButtons = { continueButton, optionsButton, quitButton };
        foreach (Button button in allButtons)
        {
            if (button == null || active.Contains(button))
            {
                continue;
            }

            Navigation none = button.navigation;
            none.mode = Navigation.Mode.None;
            button.navigation = none;
        }

        for (int i = 0; i < active.Count; i++)
        {
            Navigation nav = active[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = i > 0 ? active[i - 1] : null;
            nav.selectOnDown = i < active.Count - 1 ? active[i + 1] : null;
            nav.selectOnLeft = null;
            nav.selectOnRight = null;
            active[i].navigation = nav;
        }

        RestoreSelectionIfCurrentBecameInvalid();
    }

    private void RestoreSelectionIfCurrentBecameInvalid()
    {
        if (eventSystem == null)
        {
            return;
        }

        GameObject current = eventSystem.currentSelectedGameObject;
        bool currentIsNowDisabledOptions = optionsButton != null && current == optionsButton.gameObject && !optionsPreviewEnabled;
        bool currentIsNowDisabledQuit = quitButton != null && current == quitButton.gameObject && !quitRequestSeamEnabled;

        if (currentIsNowDisabledOptions || currentIsNowDisabledQuit)
        {
            UISelectionUtility.Select(eventSystem, continueButton);
        }
    }

    private void HandleContinueClicked()
    {
        CloseRequested?.Invoke();
    }

    private void HandleOptionsClicked()
    {
        // Package A1: production Options remains non-interactable, so this only fires when the
        // Sandbox has enabled the preview seam. Functional Options belongs to Package B; this
        // screen creates no settings data and owns no audio/display/accessibility/persistence.
        if (!optionsPreviewEnabled)
        {
            return;
        }

        OptionsRequested?.Invoke();
    }

    private void HandleQuitClicked()
    {
        // Production keeps Quit non-interactable while the seam is disabled, which already blocks
        // UGUI click events; this guard also stops any direct/programmatic invocation from ever
        // opening the confirmation modal while Package A1 has no functional Quit endpoint.
        if (!quitRequestSeamEnabled || quitConfirmationModal == null)
        {
            return;
        }

        quitConfirmationModal.Show(quitButton, continueButton, HandleQuitConfirmed, HandleQuitCancelled);
    }

    private void HandleQuitConfirmed()
    {
        if (quitRequestSeamEnabled)
        {
            QuitToMainMenuRequested?.Invoke();
        }
    }

    private void HandleQuitCancelled()
    {
        // No-op: ConfirmationModal already restores selection to the Quit button.
    }
}
