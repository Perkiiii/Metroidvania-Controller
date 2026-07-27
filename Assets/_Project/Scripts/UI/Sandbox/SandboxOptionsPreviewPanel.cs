using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Sandbox-only placeholder Options child flow (Package A1 correction pass, Finding #5). Exists
/// only in UISandbox.unity and must never appear in production <c>_GameCameras</c> composition.
/// Owns no settings data, no audio/display/accessibility state, and no AudioMixer — purely a
/// labelled placeholder panel proving the <see cref="PauseMenuScreen.OptionsRequested"/> seam and
/// focus hand-off work end to end.
/// </summary>
public sealed class SandboxOptionsPreviewPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button backButton;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private InputActionReference uiCancelActionRef;

    /// <summary>Fired when the placeholder closes via its Back button or UI/Cancel.</summary>
    public event Action CloseRequested;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    private void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(HandleBackClicked);
    }

    private void OnDestroy()
    {
        if (backButton != null) backButton.onClick.RemoveListener(HandleBackClicked);
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        InputAction cancel = uiCancelActionRef != null ? uiCancelActionRef.action : null;
        if (cancel != null && cancel.WasPressedThisFrame())
        {
            RequestClose();
        }
    }

    public void Open()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        UISelectionUtility.Select(eventSystem, backButton);
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void HandleBackClicked()
    {
        RequestClose();
    }

    private void RequestClose()
    {
        Close();
        CloseRequested?.Invoke();
    }
}
