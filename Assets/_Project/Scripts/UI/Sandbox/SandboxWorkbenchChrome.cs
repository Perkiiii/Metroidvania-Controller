using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Sandbox-only authoring chrome: compact toolbar, collapsible fixture/diagnostic drawers,
/// safe-area visibility, preview backgrounds, and automatic drawer collapse while a root is open.
/// It never enters production prefabs and never owns root flow or gameplay state.
/// </summary>
[DisallowMultipleComponent]
public sealed class SandboxWorkbenchChrome : MonoBehaviour
{
    [Header("Flow and preview")]
    [SerializeField] private UIFlowController uiFlow;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private Image previewBackground;
    [SerializeField] private RectTransform safeAreaGuide;

    [Header("Chrome")]
    [SerializeField] private GameObject fixtureDrawer;
    [SerializeField] private GameObject diagnosticsDrawer;
    [SerializeField] private TMP_Text toolbarStatus;
    [SerializeField] private Button fixtureToggleButton;
    [SerializeField] private Button diagnosticsToggleButton;
    [SerializeField] private Button safeAreaToggleButton;
    [SerializeField] private Button backgroundToggleButton;

    [Header("Behaviour")]
    [SerializeField] private bool autoCollapseChromeWhenRootOpens = true;
    [SerializeField] private bool diagnosticsHiddenByDefault = true;
    [SerializeField] private bool allowF1Toggle = true;

    private bool fixtureVisible = true;
    private bool diagnosticsVisible;
    private bool safeAreaVisible;
    private bool wasRootOpen;
    private bool restoreFixtureAfterRoot;
    private int backgroundIndex;

    private static readonly Color[] PreviewBackgrounds =
    {
        new Color(0.055f, 0.065f, 0.075f, 1f),
        new Color(0.30f, 0.31f, 0.33f, 1f),
        new Color(0.72f, 0.70f, 0.64f, 1f)
    };

    private void Awake()
    {
        diagnosticsVisible = !diagnosticsHiddenByDefault;
        WireButton(fixtureToggleButton, ToggleFixtureDrawer);
        WireButton(diagnosticsToggleButton, ToggleDiagnostics);
        WireButton(safeAreaToggleButton, ToggleSafeAreaGuide);
        WireButton(backgroundToggleButton, CyclePreviewBackground);
        ApplyChromeState();
        ApplyPreviewBackground();
    }

    private void OnDestroy()
    {
        UnwireButton(fixtureToggleButton, ToggleFixtureDrawer);
        UnwireButton(diagnosticsToggleButton, ToggleDiagnostics);
        UnwireButton(safeAreaToggleButton, ToggleSafeAreaGuide);
        UnwireButton(backgroundToggleButton, CyclePreviewBackground);
    }

    private void Update()
    {
        bool rootOpen = uiFlow != null && uiFlow.IsRootOpen;
        if (rootOpen != wasRootOpen)
        {
            if (rootOpen && autoCollapseChromeWhenRootOpens)
            {
                restoreFixtureAfterRoot = fixtureVisible;
                fixtureVisible = false;
                ApplyChromeState();
            }
            else if (!rootOpen && restoreFixtureAfterRoot)
            {
                fixtureVisible = true;
                restoreFixtureAfterRoot = false;
                ApplyChromeState();
            }

            wasRootOpen = rootOpen;
        }

        if (allowF1Toggle && Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            ToggleFixtureDrawer();
        }

        RefreshToolbarStatus();
    }

    public void ToggleFixtureDrawer()
    {
        fixtureVisible = !fixtureVisible;
        restoreFixtureAfterRoot = false;
        ApplyChromeState();
    }

    public void ToggleDiagnostics()
    {
        diagnosticsVisible = !diagnosticsVisible;
        ApplyChromeState();
    }

    public void ToggleSafeAreaGuide()
    {
        safeAreaVisible = !safeAreaVisible;
        ApplyChromeState();
    }

    public void CyclePreviewBackground()
    {
        backgroundIndex = (backgroundIndex + 1) % PreviewBackgrounds.Length;
        ApplyPreviewBackground();
    }

    private void ApplyChromeState()
    {
        if (fixtureDrawer != null)
        {
            fixtureDrawer.SetActive(fixtureVisible);
        }

        if (diagnosticsDrawer != null)
        {
            diagnosticsDrawer.SetActive(diagnosticsVisible);
        }

        if (safeAreaGuide != null)
        {
            safeAreaGuide.gameObject.SetActive(safeAreaVisible);
            Image guideImage = safeAreaGuide.GetComponent<Image>();
            if (guideImage != null)
            {
                guideImage.raycastTarget = false;
            }
        }
    }

    private void ApplyPreviewBackground()
    {
        if (previewBackground != null)
        {
            previewBackground.color = PreviewBackgrounds[Mathf.Clamp(backgroundIndex, 0, PreviewBackgrounds.Length - 1)];
            previewBackground.raycastTarget = false;
        }
    }

    private void RefreshToolbarStatus()
    {
        if (toolbarStatus == null)
        {
            return;
        }

        string root = uiFlow != null && uiFlow.IsRootOpen
            ? uiFlow.ActiveRootKind.ToString()
            : "Preview";
        string selected = eventSystem != null && eventSystem.currentSelectedGameObject != null
            ? eventSystem.currentSelectedGameObject.name
            : "none";
        toolbarStatus.text = $"UI WORKBENCH  ·  {root}  ·  focus: {selected}";
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnwireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }
}
