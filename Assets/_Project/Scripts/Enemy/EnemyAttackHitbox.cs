using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(DamageHero))]
public sealed class EnemyAttackHitbox : MonoBehaviour
{
    private const int DefaultHitBufferSize = 8;

    [SerializeField] private Collider2D hitboxCollider;
    [SerializeField] private DamageHero damageHero;
    [SerializeField] private float knockbackX = 8f;
    [SerializeField] private float knockbackY = 4f;

    private readonly Collider2D[] hitBuffer = new Collider2D[DefaultHitBufferSize];

    private void Awake()
    {
        CacheComponents();
        SetWindowActive(false);
    }

    private void OnEnable()
    {
        SetWindowActive(false);
    }

    private void OnValidate()
    {
        CacheComponents();
    }

    public void SetWindowActive(bool active)
    {
        CacheComponents();

        if (hitboxCollider == null)
        {
            return;
        }

        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = active;
    }

    public void EvaluateActiveWindow(ContactFilter2D filter, HashSet<HeroBox> damagedHeroes, Object damageSource)
    {
        if (hitboxCollider == null || damageHero == null || damageHero.DamageDealt <= 0)
        {
            return;
        }

        int hitCount = hitboxCollider.Overlap(filter, hitBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null)
            {
                continue;
            }

            HeroBox heroBox = hit.GetComponent<HeroBox>();
            if (heroBox == null)
            {
                heroBox = hit.GetComponentInParent<HeroBox>();
            }

            if (heroBox == null || !damagedHeroes.Add(heroBox))
            {
                continue;
            }

            float xDirection = heroBox.transform.position.x >= transform.position.x ? 1f : -1f;
            heroBox.TakeDamage(damageHero.DamageDealt, damageSource, new Vector2(xDirection * knockbackX, knockbackY));
        }
    }

    private void CacheComponents()
    {
        if (hitboxCollider == null)
        {
            hitboxCollider = GetComponent<Collider2D>();
        }

        if (damageHero == null)
        {
            damageHero = GetComponent<DamageHero>();
        }
    }
}
