using UnityEngine;

// Permanent shortcut gate/door (World Persistence Phase 3). Once opened, the open state is a
// serialized permanent physical fact -- it survives room transitions, checkpoints, recoverable
// hazards, normal death, and Continue, matching Docs/ImplementationPlans/WorldPersistence.md's
// "Door/shortcut -> Permanent physical fact unless domain-derived" policy. Ordinary always-usable
// scene-transition doors (TransitionPoint) remain untracked and are unaffected by this component.
//
// This component's own InteractableBase collider is the interact-range trigger only. The separate
// blockerCollider is the solid, non-trigger collider that physically blocks passage while closed.
public sealed class PersistentDoor : InteractableBase
{
    private const string StateClosed = "closed";
    private const string StateOpen = "open";

    [Tooltip("Stable identity for this placed door instance. Must be unique across ALL persistent " +
        "world objects (enemies, pickups, doors, switches, breakables) and must not be reused when " +
        "duplicating a prefab instance in the Editor.")]
    [SerializeField] private string worldObjectId;

    [SerializeField] private WorldStateRegistry registry;

    [Tooltip("Solid, non-trigger collider that physically blocks passage while closed. Disabled once opened.")]
    [SerializeField] private Collider2D blockerCollider;

    [Tooltip("Optional visual shown only while closed.")]
    [SerializeField] private GameObject closedVisualRoot;

    [Tooltip("Optional visual shown only while open.")]
    [SerializeField] private GameObject openVisualRoot;

    private bool isOpen;

    private void Awake()
    {
        ReconcileOnInitialization();
    }

    // Called once on initialization, before the door can be interacted with.
    private void ReconcileOnInitialization()
    {
        if (registry != null && string.IsNullOrEmpty(worldObjectId))
            Debug.LogError($"[PersistentDoor] '{name}' has a WorldStateRegistry assigned but no worldObjectId. Treating as closed (no persistence possible).", this);

        bool storedOpen = QueryStoredOpenState();
        ApplyOpenState(storedOpen, playFeedback: false);
    }

    private bool QueryStoredOpenState()
    {
        if (registry == null || string.IsNullOrEmpty(worldObjectId))
            return false;

        if (!registry.TryGetObjectState(worldObjectId, out string state))
            return false;

        return NormalizeState(state);
    }

    private bool NormalizeState(string state)
    {
        if (state == StateOpen) return true;
        if (state == StateClosed) return false;

        Debug.LogWarning($"[PersistentDoor] '{name}' (id '{worldObjectId}') had unrecognized stored state '{state}'; falling back to closed.", this);
        return false;
    }

    // Quiet-state application. playFeedback must be false during restoration and true only for a
    // genuine, confirmed opening -- no VFX/audio system is wired for doors in this milestone, so the
    // flag currently only gates SetDisabled/no-op, but the seam exists for a future feedback pass.
    private void ApplyOpenState(bool open, bool playFeedback)
    {
        isOpen = open;

        if (blockerCollider != null)
            blockerCollider.enabled = !open;

        if (closedVisualRoot != null)
            closedVisualRoot.SetActive(!open);

        if (openVisualRoot != null)
            openVisualRoot.SetActive(open);

        if (open)
            SetDisabled(true); // an open shortcut door has nothing left to interact with

        _ = playFeedback;
    }

    public override void Interact()
    {
        if (isOpen) return;

        ApplyOpenState(true, playFeedback: true);
        RecordOpened();
    }

    private void RecordOpened()
    {
        if (registry == null || string.IsNullOrEmpty(worldObjectId))
            return;

        registry.SetObjectState(worldObjectId, StateOpen);
    }
}
