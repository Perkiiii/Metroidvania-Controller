using UnityEngine;

/// <summary>Presentation-only horizontal view of persistent resource parts.</summary>
public sealed class ResourceDisplay : MonoBehaviour
{
    [SerializeField] private PlayerResourceState resourceState;
    [SerializeField] private ResourceBarView barView;
    [SerializeField] private CanvasGroup visibilityGroup;

    private bool subscribed;
    private bool hasLastChange;
    private PlayerResourceChangeReason lastChangeReason;
    private int currentParts;
    private int maximumParts;
    private float fillAmount01;

    public int CurrentParts => currentParts;
    public int MaximumParts => maximumParts;
    public float FillAmount01 => fillAmount01;
    public bool IsVisible => true;
    public bool HasLastChangeReason => hasLastChange;
    public PlayerResourceChangeReason LastChangeReason => lastChangeReason;
    public int GameplayFeedbackCount { get; private set; }
    public bool HasValidDependencies => resourceState != null;

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    public void Configure(PlayerResourceState state)
    {
        if (resourceState != state)
        {
            Unsubscribe();
            resourceState = state;
        }

        Subscribe();
        Refresh();
    }

    // Retained for callers authored before the bar refactor. Parts-per-pip is no
    // longer a presentation dependency, so the config is intentionally ignored.
    public void Configure(PlayerResourceState state, PlayerResourceConfig ignoredConfig)
    {
        Configure(state);
    }

    public void Refresh()
    {
        if (resourceState == null)
            return;

        ApplySnapshot(resourceState.CurrentParts, resourceState.MaximumParts,
            PlayerResourceChangeReason.StateApplied, false);
    }

    private void Subscribe()
    {
        if (!isActiveAndEnabled || subscribed || resourceState == null)
            return;

        resourceState.Changed += OnResourceChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || resourceState == null)
            return;

        resourceState.Changed -= OnResourceChanged;
        subscribed = false;
    }

    private void OnResourceChanged(PlayerResourceChangeInfo info)
    {
        bool gameplayChange = info.Reason != PlayerResourceChangeReason.StateApplied
            && info.Reason != PlayerResourceChangeReason.Reset;
        ApplySnapshot(info.CurrentParts, info.MaximumParts, info.Reason, gameplayChange);
    }

    private void ApplySnapshot(int nextCurrent, int nextMaximum,
        PlayerResourceChangeReason reason, bool gameplayChange)
    {
        maximumParts = Mathf.Max(0, nextMaximum);
        currentParts = Mathf.Clamp(nextCurrent, 0, maximumParts);
        fillAmount01 = maximumParts > 0 ? (float)currentParts / maximumParts : 0f;
        lastChangeReason = reason;
        hasLastChange = true;

        if (gameplayChange)
            GameplayFeedbackCount++;

        if (visibilityGroup != null)
        {
            visibilityGroup.alpha = 1f;
            visibilityGroup.interactable = false;
            visibilityGroup.blocksRaycasts = false;
        }

        if (barView != null)
        {
            ResourceBarChangePresentation presentation = ResourceBarChangePresentation.Snap;
            if (gameplayChange)
            {
                presentation = reason == PlayerResourceChangeReason.Gain
                    ? ResourceBarChangePresentation.Gain
                    : reason == PlayerResourceChangeReason.Cleared
                        ? ResourceBarChangePresentation.Cleared
                        : ResourceBarChangePresentation.Spend;
            }

            barView.SetFill(fillAmount01, presentation);
        }
    }
}
