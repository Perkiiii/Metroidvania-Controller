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

    // Editor-only authoring aid: authored hitboxes are disabled outside their active window, so
    // without this their geometry is otherwise invisible in the Scene view unless briefly enabled
    // by hand. Green while enabled (window active), grey while authored-disabled (the normal
    // resting state). Never called outside the Scene view.
    private void OnDrawGizmosSelected()
    {
        CacheComponents();
        if (hitboxCollider == null)
        {
            return;
        }

        Bounds bounds = hitboxCollider.bounds;
        Gizmos.color = hitboxCollider.enabled
            ? new Color(0.2f, 1f, 0.2f, 0.6f)
            : new Color(0.6f, 0.6f, 0.6f, 0.5f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
