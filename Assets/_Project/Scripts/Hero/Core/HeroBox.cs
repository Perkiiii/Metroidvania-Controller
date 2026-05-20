using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class HeroBox : MonoBehaviour
{
    private HeroHealthComponent health;
    private bool hasPendingDamage;
    private int pendingDamage;
    private object pendingSource;
    private Vector2 pendingKnockback;

    public HeroHealthComponent Health => health;

    private void Awake()
    {
        health = GetComponentInParent<HeroHealthComponent>();
        if (health == null)
        {
            Debug.LogError($"HeroBox on '{name}' could not find a parent HeroHealthComponent.", this);
        }
    }

    private void FixedUpdate()
    {
        if (!hasPendingDamage)
        {
            return;
        }

        int damage = pendingDamage;
        object source = pendingSource;
        Vector2 knockback = pendingKnockback;
        ClearPendingDamage();

        health?.TakeDamage(damage, source, knockback);
    }

    public void TakeDamage(int amount, object source, Vector2 knockback)
    {
        if (amount <= 0)
        {
            return;
        }

        if (!hasPendingDamage || amount >= pendingDamage)
        {
            // Equal damage keeps the latest source/knockback so contact order has a stable, simple rule.
            hasPendingDamage = true;
            pendingDamage = amount;
            pendingSource = source;
            pendingKnockback = knockback;
        }
    }

    public void TriggerHazardDeath()
    {
        ClearPendingDamage();
        health?.TriggerHazardDeath();
    }

    public void HandleHazard(HazardContact contact)
    {
        // Hazards take priority over same-step enemy/contact damage buffered for FixedUpdate.
        ClearPendingDamage();

        if (GameManager.Instance != null && GameManager.Instance.IsRespawnOrRecoveryInProgress)
            return;

        if (contact.RecoveryMode == HazardRecoveryMode.InstantDeath)
        {
            health?.TriggerHazardDeath();
            return;
        }

        // Guard before touching health: RecoverLocal requires GameManager to run the recovery
        // sequence. Applying damage without recovery would leave the hero in a stuck Hurt state.
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[HeroBox] Recoverable hazard hit ignored — GameManager is missing. Ensure the boot scene is used; direct scene testing bypasses the boot flow.", this);
            return;
        }

        if (health == null)
            return;

        DamageResult result = health.TakeHazardDamage(contact.Damage, contact.Source);
        if (result.WasIgnored || result.IsFatal)
            return;

        GameManager.Instance.BeginHazardRecoverySequence(contact);
    }

    private void ClearPendingDamage()
    {
        hasPendingDamage = false;
        pendingDamage = 0;
        pendingSource = null;
        pendingKnockback = Vector2.zero;
    }
}
