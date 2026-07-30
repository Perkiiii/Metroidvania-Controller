using TMPro;
using UnityEngine;

/// <summary>
/// Sandbox-only visible proof that <see cref="PauseMenuScreen.QuitToMainMenuRequested"/> actually
/// fires once the Sandbox enables the typed Quit request seam (Package A1 correction pass,
/// Finding #4). Does not load a scene, save, run Boot, or alter any production state — purely a
/// status label driven by the real shared confirmation modal's confirmed callback.
/// </summary>
public sealed class SandboxQuitCallbackStatus : MonoBehaviour
{
    [SerializeField] private PauseMenuScreen pauseMenuScreen;
    [SerializeField] private TMP_Text statusText;

    private int firedCount;

    private void OnEnable()
    {
        if (pauseMenuScreen != null) pauseMenuScreen.QuitToMainMenuRequested += HandleQuitRequested;
        Refresh();
    }

    private void OnDisable()
    {
        if (pauseMenuScreen != null) pauseMenuScreen.QuitToMainMenuRequested -= HandleQuitRequested;
    }

    private void HandleQuitRequested()
    {
        firedCount++;
        Refresh();
    }

    private void Refresh()
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = firedCount == 0
            ? "Quit callback: not fired yet"
            : "Quit callback fired (" + firedCount + ")";
    }
}
