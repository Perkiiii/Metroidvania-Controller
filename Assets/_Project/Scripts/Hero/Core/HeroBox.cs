using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class HeroBox : MonoBehaviour
{
    private HeroHealthComponent health;

    public HeroHealthComponent Health => health;

    private void Awake()
    {
        health = GetComponentInParent<HeroHealthComponent>();
        if (health == null)
        {
            Debug.LogError($"HeroBox on '{name}' could not find a parent HeroHealthComponent.", this);
        }
    }

    public void TakeDamage(int amount, object source, Vector2 knockback)
    {
        health?.TakeDamage(amount, source, knockback);
    }

    public void TriggerHazardDeath()
    {
        health?.TriggerHazardDeath();
    }
}
