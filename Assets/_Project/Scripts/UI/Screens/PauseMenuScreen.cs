using System;
using UnityEngine;
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

    public Selectable FirstSelection => continueButton;
    public bool HasOpenModal => quitConfirmationModal != null && quitConfirmationModal.IsOpen;

    private void Awake()
    {
        if (continueButton != null) continueButton.onClick.AddListener(HandleContinueClicked);
        if (optionsButton != null) optionsButton.onClick.AddListener(HandleOptionsClicked);
        if (quitButton != null) quitButton.onClick.AddListener(HandleQuitClicked);

        ApplyOptionsGate();
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
        ApplyOptionsGate();
    }

    /// <summary>Sandbox-only: enable the typed Quit request seam for verification.</summary>
    public void SetQuitRequestSeamEnabled(bool enabled)
    {
        quitRequestSeamEnabled = enabled;
    }

    private void ApplyOptionsGate()
    {
        if (optionsButton != null)
        {
            optionsButton.interactable = optionsPreviewEnabled;
        }
    }

    private void HandleContinueClicked()
    {
        CloseRequested?.Invoke();
    }

    private void HandleOptionsClicked()
    {
        // Package A1: authored button/position only. Functional Options belongs to Package B.
        // The Sandbox-only preview path (optionsPreviewEnabled) is exercised entirely through
        // SetOptionsPreviewEnabled; there is no production child screen to open here.
    }

    private void HandleQuitClicked()
    {
        if (quitConfirmationModal == null)
        {
            return;
        }

        quitConfirmationModal.Show(quitButton, HandleQuitConfirmed, HandleQuitCancelled);
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
