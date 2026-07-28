using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sandbox-only developer recovery controls (Package A2 correction pass, BUG 3/4). Lives on its own
/// Canvas above the production Root and Modal layers so it stays clickable while a full-screen
/// pausing root correctly blocks raycasts to everything underneath it — including the Sandbox's own
/// fixture panel. That blocking is the production behaviour under test, so the recovery path has to
/// sit above it rather than weaken it.
///
/// Exists only in <c>UISandbox.unity</c>. <c>UIFoundationValidator</c> and
/// <c>GameplayMenuProductionAssetTests</c> both reject this component inside
/// <c>_GameCameras.prefab</c>.
///
/// It owns no gameplay state, never writes <c>Time.timeScale</c>, and never calls
/// <c>GameManager</c>. Its primary action is the ordinary
/// <see cref="UIFlowController.RequestCloseRoot"/> path, so using it exercises the same close
/// sequence the player's Continue/Close/Cancel routes use. The recovery fallback is a host-gated
/// seam on UIFlowController, so even an authoring recovery cannot make this layer a second root
/// owner.
/// </summary>
[DisallowMultipleComponent]
public sealed class SandboxDeveloperUtilityLayer : MonoBehaviour
{
    [Header("Flow under test")]
    [SerializeField] private UIFlowController uiFlow;

    [Header("Controls")]
    [Tooltip("Requests the normal UIFlowController close sequence for whichever root is open.")]
    [SerializeField] private Button emergencyCloseButton;

    [Tooltip("Last-resort recovery when the flow controller reports nothing to close but a root " +
        "screen is still visible — i.e. the composition itself is misauthored.")]
    [SerializeField] private Button forceHideRootsButton;

    [Header("Readout")]
    [SerializeField] private Text statusLabel;

    private string lastAction = "none yet";

    private void Awake()
    {
        if (emergencyCloseButton != null) emergencyCloseButton.onClick.AddListener(RequestNormalClose);
        if (forceHideRootsButton != null) forceHideRootsButton.onClick.AddListener(ForceHideRoots);
        RefreshStatus();
    }

    private void OnDestroy()
    {
        if (emergencyCloseButton != null) emergencyCloseButton.onClick.RemoveListener(RequestNormalClose);
        if (forceHideRootsButton != null) forceHideRootsButton.onClick.RemoveListener(ForceHideRoots);
    }

    private void Update()
    {
        RefreshStatus();
    }

    /// <summary>
    /// Closes the active root through the production close sequence — the same one Continue, the
    /// Gameplay Menu's Close control, Back/Cancel, and the open-action toggle run.
    /// </summary>
    public void RequestNormalClose()
    {
        if (uiFlow == null)
        {
            lastAction = "no UIFlowController assigned";
            return;
        }

        lastAction = uiFlow.RequestCloseRoot()
            ? "closed through UIFlowController"
            : "nothing stably open to close";
    }

    /// <summary>
    /// Sandbox-only escape hatch for a visual/flow authoring mismatch. UIFlowController still owns
    /// the reset; this component never calls a root screen's Hide method itself.
    /// </summary>
    public void ForceHideRoots()
    {
        if (uiFlow == null)
        {
            lastAction = "no UIFlowController assigned";
            return;
        }

        bool recovered = uiFlow.RequestHostRecoveryClose();
        lastAction = recovered
            ? "recovered through Sandbox flow seam"
            : "recovery refused (no Sandbox host)";

        if (!recovered)
        {
            return;
        }

        Debug.LogWarning(
            "[SandboxDeveloperUtilityLayer] Used the Sandbox host recovery seam. If this was needed, " +
            "the composition or normal close routing is misauthored.",
            this);
    }

    private void RefreshStatus()
    {
        if (statusLabel == null)
        {
            return;
        }

        string root = uiFlow == null
            ? "no flow"
            : uiFlow.IsRootOpen ? uiFlow.ActiveRootKind.ToString() : "closed";

        statusLabel.text = $"Root: {root}   timeScale: {Time.timeScale:0.##}\nLast dev action: {lastAction}";
    }
}
