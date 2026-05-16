using System;
using Animancer;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroAttackModule : MonoBehaviour
{
    [Header("Identity")]
    public HeroAttackDirection direction = HeroAttackDirection.Side;
    public bool mirrorWithFacing;

    [Header("Colliders")]
    public PolygonCollider2D damageCollider;
    public PolygonCollider2D clashCollider;
    public LayerMask damageLayers;
    public LayerMask clashLayers;

    [Header("Presentation")]
    public Transform visualRoot;
    public AnimancerComponent visualAnimancer;
    public AnimationClip visualClip;
    public SpriteRenderer spriteRenderer;
    public AudioSource audioSource;
    public AudioClip slashClip;

    private Vector3 initialLocalPosition;
    private Vector3 initialLocalEulerAngles;
    private Vector3 initialLocalScale;
    private Action beginAttackWindow;
    private Action endAttackWindow;
    private bool initialized;

    public Collider2D DamageCollider => damageCollider;
    public Collider2D ClashCollider => clashCollider;
    public bool HasDamageCollider => damageCollider != null;
    public bool HasClashCollider => clashCollider != null;

    private void Awake()
    {
        Initialize();
        Deactivate();
    }

    private void OnDisable()
    {
        SetHitWindowActive(false);
        StopVisualAnimation();
        SetVisualActive(false);
    }

    private void OnValidate()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (damageCollider == null)
        {
            damageCollider = FindDamageCollider();
        }

        if (visualRoot == null && spriteRenderer != null)
        {
            visualRoot = spriteRenderer.transform;
        }

        if (visualAnimancer == null)
        {
            visualAnimancer = FindVisualAnimancer();
        }
    }

    public void Activate(int facingDirection)
    {
        Initialize();
        ApplyFacing(facingDirection);
        SetHitWindowActive(false);
        SetVisualActive(true);
        PlayVisualAnimation();

        if (slashClip != null)
        {
            AudioManager.Instance?.PlaySFX(slashClip);
        }
    }

    public void Deactivate()
    {
        Initialize();
        SetHitWindowActive(false);
        StopVisualAnimation();
        SetVisualActive(false);
        ClearAttackWindowCallbacks();
    }

    public void SetAttackWindowCallbacks(Action beginWindow, Action endWindow)
    {
        beginAttackWindow = beginWindow;
        endAttackWindow = endWindow;
    }

    public void ClearAttackWindowCallbacks()
    {
        beginAttackWindow = null;
        endAttackWindow = null;
    }

    public void BeginAttackWindow()
    {
        beginAttackWindow?.Invoke();
    }

    public void EndAttackWindow()
    {
        endAttackWindow?.Invoke();
    }

    public void SetHitWindowActive(bool active)
    {
        if (damageCollider != null)
        {
            damageCollider.enabled = active;
            damageCollider.isTrigger = true;
        }

        if (clashCollider != null)
        {
            clashCollider.enabled = active;
            clashCollider.isTrigger = true;
        }
    }

    public ContactFilter2D CreateDamageFilter(LayerMask fallbackLayers)
    {
        return CreateFilter(damageLayers.value != 0 ? damageLayers : fallbackLayers);
    }

    public ContactFilter2D CreateClashFilter(LayerMask fallbackLayers)
    {
        return CreateFilter(clashLayers.value != 0 ? clashLayers : fallbackLayers);
    }

    public Vector2 GetDamageReferencePoint()
    {
        if (damageCollider != null)
        {
            return damageCollider.bounds.center;
        }

        return transform.position;
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (damageCollider == null)
        {
            damageCollider = FindDamageCollider();
        }

        if (visualRoot == null && spriteRenderer != null)
        {
            visualRoot = spriteRenderer.transform;
        }

        if (visualAnimancer == null)
        {
            visualAnimancer = FindVisualAnimancer();
        }

        initialLocalPosition = transform.localPosition;
        initialLocalEulerAngles = transform.localEulerAngles;
        initialLocalScale = transform.localScale;
        initialized = true;
    }

    private void ApplyFacing(int facingDirection)
    {
        if (!mirrorWithFacing)
        {
            return;
        }

        int sign = facingDirection < 0 ? -1 : 1;
        Vector3 position = initialLocalPosition;
        Vector3 rotation = initialLocalEulerAngles;
        Vector3 scale = initialLocalScale;
        position.x = initialLocalPosition.x * sign;
        rotation.z = initialLocalEulerAngles.z * sign;
        scale.x = initialLocalScale.x * sign;
        transform.localPosition = position;
        transform.localEulerAngles = rotation;
        transform.localScale = scale;
    }

    private void SetVisualActive(bool active)
    {
        if (visualRoot != null && visualRoot != transform)
        {
            visualRoot.gameObject.SetActive(active);
            return;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = active;
        }
    }

    private void PlayVisualAnimation()
    {
        if (visualClip == null)
        {
            return;
        }

        EnsureVisualAnimancer();

        if (visualAnimancer == null)
        {
            return;
        }

        visualAnimancer.Play(visualClip, 0f, FadeMode.FromStart);
    }

    private void StopVisualAnimation()
    {
        if (visualAnimancer != null)
        {
            visualAnimancer.Stop();
        }
    }

    private void EnsureVisualAnimancer()
    {
        if (visualAnimancer != null)
        {
            EnsureVisualAnimator(visualAnimancer.gameObject);
            return;
        }

        visualAnimancer = FindVisualAnimancer();

        if (visualAnimancer != null)
        {
            EnsureVisualAnimator(visualAnimancer.gameObject);
        }
        else if (visualClip != null && Application.isPlaying)
        {
            GameObject target = visualRoot != null ? visualRoot.gameObject : gameObject;
            EnsureVisualAnimator(target);
            visualAnimancer = target.AddComponent<AnimancerComponent>();
        }
    }

    private void EnsureVisualAnimator(GameObject target)
    {
        if (target == null || !Application.isPlaying)
        {
            return;
        }

        Animator animator = target.GetComponent<Animator>();
        if (animator == null)
        {
            animator = target.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = null;

        if (visualAnimancer != null && visualAnimancer.Animator == null)
        {
            visualAnimancer.Animator = animator;
        }
    }

    private AnimancerComponent FindVisualAnimancer()
    {
        if (visualRoot != null)
        {
            AnimancerComponent found = visualRoot.GetComponent<AnimancerComponent>();
            if (found != null)
            {
                return found;
            }

            found = visualRoot.GetComponentInChildren<AnimancerComponent>(true);
            if (found != null)
            {
                return found;
            }
        }

        return GetComponentInChildren<AnimancerComponent>(true);
    }

    private PolygonCollider2D FindDamageCollider()
    {
        if (spriteRenderer != null)
        {
            PolygonCollider2D spriteCollider = spriteRenderer.GetComponent<PolygonCollider2D>();
            if (spriteCollider != null)
            {
                return spriteCollider;
            }
        }

        PolygonCollider2D localCollider = GetComponent<PolygonCollider2D>();
        if (localCollider != null)
        {
            return localCollider;
        }

        return GetComponentInChildren<PolygonCollider2D>(true);
    }

    private static ContactFilter2D CreateFilter(LayerMask layers)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(layers);
        return filter;
    }
}
