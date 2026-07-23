using UnityEngine;

// Switch/lever whose activated state may be RoomRuntime (authored default only -- never queries or
// writes the registry), UntilDeath (transient, cleared on normal death, persists across room
// transitions/checkpoints/hazards), or Permanent (serialized). This is a one-shot switch: once
// activated it stays activated for the remainder of its lifetime scope. The switch only stores and
// applies the state fact -- the paired blocker/gate it controls is a plain scene object it directly
// toggles, not a generic mechanism/event framework (World Persistence Phase 3).
public sealed class PersistentSwitch : InteractableBase
{
    private const string StateInactive = "inactive";
    private const string StateActive = "active";

    [Tooltip("Stable identity for this placed switch instance. Required for UntilDeath/Permanent " +
        "lifetimes; ignored for RoomRuntime. Must be unique across ALL persistent world objects " +
        "(enemies, pickups, doors, switches, breakables) and must not be reused when duplicating a " +
        "prefab instance in the Editor.")]
    [SerializeField] private string worldObjectId;

    [SerializeField] private WorldStateRegistry registry;

    [SerializeField] private PersistenceLifetime lifetime = PersistenceLifetime.UntilDeath;

    [Tooltip("Solid, non-trigger collider on the paired gate/blocker this switch opens. Disabled once activated.")]
    [SerializeField] private Collider2D blockerCollider;

    [Tooltip("Optional visual shown only while the paired blocker is closed.")]
    [SerializeField] private GameObject blockerClosedVisualRoot;

    [Tooltip("Optional visual shown only while the paired blocker is open.")]
    [SerializeField] private GameObject blockerOpenVisualRoot;

    private bool isActive;

    private void Awake()
    {
        ReconcileOnInitialization();
    }

    // Called once on initialization, before this switch can be interacted with.
    private void ReconcileOnInitialization()
    {
        if (registry != null && lifetime != PersistenceLifetime.RoomRuntime && string.IsNullOrEmpty(worldObjectId))
            Debug.LogError($"[PersistentSwitch] '{name}' has a WorldStateRegistry assigned and a {lifetime} lifetime but no worldObjectId. Treating as inactive (no persistence possible).", this);

        bool storedActive = QueryStoredActiveState();
        ApplyActivatedState(storedActive, playFeedback: false);
    }

    private bool QueryStoredActiveState()
    {
        if (registry == null || lifetime == PersistenceLifetime.RoomRuntime)
            return false;

        bool found;
        string state;
        if (lifetime == PersistenceLifetime.UntilDeath)
            found = registry.TryGetUntilDeathState(worldObjectId, out state);
        else
            found = registry.TryGetObjectState(worldObjectId, out state);

        return found && NormalizeState(state);
    }

    private bool NormalizeState(string state)
    {
        if (state == StateActive) return true;
        if (state == StateInactive) return false;

        Debug.LogWarning($"[PersistentSwitch] '{name}' (id '{worldObjectId}') had unrecognized stored state '{state}'; falling back to inactive.", this);
        return false;
    }

    // Quiet-state application. playFeedback must be false during restoration and true only for a
    // genuine, confirmed activation -- no pull-effect/audio/camera-shake/reward system is wired for
    // switches in this milestone, so the flag currently only gates SetDisabled/no-op.
    private void ApplyActivatedState(bool active, bool playFeedback)
    {
        isActive = active;

        if (blockerCollider != null)
            blockerCollider.enabled = !active;

        if (blockerClosedVisualRoot != null)
            blockerClosedVisualRoot.SetActive(!active);

        if (blockerOpenVisualRoot != null)
            blockerOpenVisualRoot.SetActive(active);

        if (active)
            SetDisabled(true); // an activated one-shot switch has nothing left to interact with

        _ = playFeedback;
    }

    public override void Interact()
    {
        if (isActive) return;

        ApplyActivatedState(true, playFeedback: true);
        RecordActivated();
    }

    private void RecordActivated()
    {
        if (registry == null || string.IsNullOrEmpty(worldObjectId))
            return;

        switch (lifetime)
        {
            case PersistenceLifetime.RoomRuntime:
                break; // authored-default-only; writes nothing
            case PersistenceLifetime.UntilDeath:
                registry.SetUntilDeathState(worldObjectId, StateActive);
                break;
            case PersistenceLifetime.Permanent:
                registry.SetObjectState(worldObjectId, StateActive);
                break;
        }
    }
}
