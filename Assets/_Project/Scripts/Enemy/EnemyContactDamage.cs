using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(DamageHero))]
public sealed class EnemyContactDamage : MonoBehaviour
{
    [SerializeField] private float damageCooldown = 0.5f;
    [SerializeField] private float knockbackX = 8f;
    [SerializeField] private float knockbackY = 4f;

    private DamageHero damageHero;
    private EnemyStateBlackboard blackboard;
    private float cooldownRemaining;

    private void Awake()
    {
        damageHero = GetComponent<DamageHero>();
        blackboard = GetComponentInParent<EnemyStateBlackboard>();
    }

    private void OnEnable()
    {
        cooldownRemaining = 0f;
    }

    private void Update()
    {
        if (cooldownRemaining > 0f)
        {
            cooldownRemaining -= Time.deltaTime;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDamageHero(collision.collider);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamageHero(other);
    }

    private void TryDamageHero(Collider2D other)
    {
        if (damageHero == null || damageHero.DamageDealt <= 0 || cooldownRemaining > 0f)
        {
            return;
        }

        if (blackboard != null && blackboard.dead)
        {
            return;
        }

        HeroBox heroBox = other.GetComponent<HeroBox>();
        if (heroBox == null)
        {
            return;
        }

        float xDirection = other.transform.position.x >= transform.position.x ? 1f : -1f;
        heroBox.TakeDamage(damageHero.DamageDealt, this, new Vector2(xDirection * knockbackX, knockbackY));
        cooldownRemaining = damageCooldown;
    }
}
