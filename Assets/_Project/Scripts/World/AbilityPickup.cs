using UnityEngine;

// Physical pickup that grants an ability. PlayerAbilityState remains the only authority for
// ability ownership; WorldStateRegistry only records that this physical pickup was consumed.
// Inconsistent ability/pickup data is reconciled in favor of PlayerAbilityState.
public sealed class AbilityPickup : MonoBehaviour
{
    [Tooltip("Stable identity for this placed pickup instance. Must be unique per scene and must " +
        "not be reused when duplicating a prefab instance in the Editor.")]
    [SerializeField] private string worldObjectId;

    [SerializeField] private PlayerAbilityState abilityState;
    [SerializeField] private AbilityId ability;
    [SerializeField] private WorldStateRegistry registry;
    [SerializeField] private bool disableAfterPickup = true;

    private void Awake()
    {
        ReconcileOnInitialization();
    }

    // Called once on initialization, before the pickup can be collected.
    private void ReconcileOnInitialization()
    {
        if (abilityState == null || registry == null)
            return;

        if (string.IsNullOrEmpty(worldObjectId))
        {
            Debug.LogError($"[AbilityPickup] '{name}' has a WorldStateRegistry assigned but no worldObjectId. Treating as active (no suppression possible).", this);
            return;
        }

        bool abilityUnlocked = abilityState.IsUnlocked(ability);
        bool pickupConsumed = registry.IsPickupCollected(worldObjectId);

        if (abilityUnlocked)
        {
            // Reconcile a missing pickup record without replaying acquisition feedback.
            if (!pickupConsumed)
                registry.MarkPickupCollected(worldObjectId);

            gameObject.SetActive(false);
            return;
        }

        if (pickupConsumed)
        {
            // Inconsistent: pickup marked consumed but the ability was never granted.
            // PlayerAbilityState is authoritative, so the stale consumed record is cleared and the
            // pickup stays collectible rather than permanently locking the player out of the
            // ability. Never unlock the ability, fire AbilityChanged, or replay collection feedback
            // here -- only an actual collection through OnTriggerEnter2D may do that.
            Debug.LogWarning($"[AbilityPickup] '{name}' (id '{worldObjectId}') was marked consumed in WorldStateRegistry but its ability is not unlocked. Clearing the stale record so it can be reacquired.", this);
            registry.ClearPickupCollectedRecord(worldObjectId);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        bool isHero = other.GetComponentInParent<HeroBox>() != null
                   || other.GetComponentInParent<HeroController>() != null;
        if (!isHero)
            return;

        if (abilityState == null)
        {
            Debug.LogWarning($"{nameof(AbilityPickup)} on {name} has no PlayerAbilityState assigned.", this);
            return;
        }

        abilityState.Unlock(ability);

        if (registry != null && !string.IsNullOrEmpty(worldObjectId))
            registry.MarkPickupCollected(worldObjectId);

        if (disableAfterPickup)
            gameObject.SetActive(false);
    }
}
