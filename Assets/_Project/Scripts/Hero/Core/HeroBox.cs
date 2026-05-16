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

    private void ClearPendingDamage()
    {
        hasPendingDamage = false;
        pendingDamage = 0;
        pendingSource = null;
        pendingKnockback = Vector2.zero;
    }
}
