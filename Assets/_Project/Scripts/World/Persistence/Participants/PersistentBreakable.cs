using UnityEngine;

// Environmental breakable whose destroyed state may be RoomRuntime (resets every scene load -- the
// default for ordinary breakables), UntilDeath (persists across room transitions, cleared on normal
// death), or Permanent (serialized, survives death and Continue). The registry stores only the
// physical fact that this specific instance is destroyed -- it never owns rewards, drops, VFX, or
// audio; those remain out of scope for this milestone (World Persistence Phase 3).
[DisallowMultipleComponent]
public sealed class PersistentBreakable : MonoBehaviour, IHeroAttackReceiver
{
    private const string StateIntact = "intact";
    private const string StateDestroyed = "destroyed";

    [Tooltip("Stable identity for this placed breakable instance. Required for UntilDeath/Permanent " +
        "lifetimes; ignored for RoomRuntime. Must be unique across ALL persistent world objects " +
        "(enemies, pickups, doors, switches, breakables) and must not be reused when duplicating a " +
        "prefab instance in the Editor.")]
    [SerializeField] private string worldObjectId;

    [SerializeField] private WorldStateRegistry registry;

    [SerializeField] private PersistenceLifetime lifetime = PersistenceLifetime.RoomRuntime;

    [Tooltip("Solid, non-trigger collider that blocks passage and receives hero attacks while intact.")]
    [SerializeField] private Collider2D solidCollider;

    [Tooltip("Visual shown only while intact.")]
    [SerializeField] private GameObject intactVisualRoot;

    [Tooltip("Optional visual (e.g. rubble) shown only once destroyed.")]
    [SerializeField] private GameObject destroyedVisualRoot;

    private bool isDestroyed;

    private void Awake()
    {
        ReconcileOnInitialization();
    }

    // Called once on initialization, before this breakable can receive hero attacks.
    private void ReconcileOnInitialization()
    {
        if (registry != null && lifetime != PersistenceLifetime.RoomRuntime && string.IsNullOrEmpty(worldObjectId))
            Debug.LogError($"[PersistentBreakable] '{name}' has a WorldStateRegistry assigned and a {lifetime} lifetime but no worldObjectId. Treating as intact (no persistence possible).", this);

        bool storedDestroyed = QueryStoredDestroyedState();
        ApplyDestroyedState(storedDestroyed, playFeedback: false);
    }

    private bool QueryStoredDestroyedState()
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
        if (state == StateDestroyed) return true;
        if (state == StateIntact) return false;

        Debug.LogWarning($"[PersistentBreakable] '{name}' (id '{worldObjectId}') had unrecognized stored state '{state}'; falling back to intact.", this);
        return false;
    }

    // Quiet-state application. playFeedback must be false during restoration and true only for a
    // genuine, confirmed destruction -- no VFX/audio/drop system is wired for breakables in this
    // milestone, so the flag is currently a documented no-op seam for a future feedback pass.
    private void ApplyDestroyedState(bool destroyed, bool playFeedback)
    {
        isDestroyed = destroyed;

        if (solidCollider != null)
            solidCollider.enabled = !destroyed;

        if (intactVisualRoot != null)
            intactVisualRoot.SetActive(!destroyed);

        if (destroyedVisualRoot != null)
            destroyedVisualRoot.SetActive(destroyed);

        _ = playFeedback;
    }

    public HeroAttackResult ReceiveHeroAttack(HeroAttackHit hit)
    {
        if (isDestroyed || hit.Damage <= 0)
            return HeroAttackResult.Ignored;

        ApplyDestroyedState(true, playFeedback: true);
        RecordDestroyed();
        return HeroAttackResult.Damaged(hit.Damage);
    }

    private void RecordDestroyed()
    {
        if (registry == null || string.IsNullOrEmpty(worldObjectId))
            return;

        switch (lifetime)
        {
            case PersistenceLifetime.RoomRuntime:
                break; // resets every scene load; writes nothing
            case PersistenceLifetime.UntilDeath:
                registry.SetUntilDeathState(worldObjectId, StateDestroyed);
                break;
            case PersistenceLifetime.Permanent:
                registry.SetObjectState(worldObjectId, StateDestroyed);
                break;
        }
    }
}
