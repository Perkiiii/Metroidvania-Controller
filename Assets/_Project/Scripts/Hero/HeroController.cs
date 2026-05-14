using System.Collections;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class HeroController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private HeroConfig config;
    [SerializeField] private HeroAnimationLibrary animationLibrary;
    [SerializeField] private Transform spriteRoot;

    private readonly HashSet<object> controlLocks = new HashSet<object>();

    private HeroStateBlackboard blackboard;
    private HeroInputReader inputReader;
    private HeroSensors sensors;
    private HeroMotor motor;
    private HeroActionController actions;
    private HeroAnimationController animations;
    private HeroHealthComponent health;
    private SpriteFlash flasher;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private AnimancerComponent animancer;

    public bool IsGrounded => blackboard != null && blackboard.grounded;
    public bool IsAirborne => !IsGrounded;
    public int FacingDirection => blackboard != null ? blackboard.FacingDirection : 1;

    protected virtual void Awake()
    {
        ResolveDependencies();
        InitializeSystems();
    }

    protected virtual void Update()
    {
        inputReader.Tick();
        actions.Tick();
    }

    protected virtual void FixedUpdate()
    {
        float fixedDeltaTime = Time.fixedDeltaTime;
        sensors.FixedTick();
        actions.FixedTick(fixedDeltaTime);
        motor.FixedTick(fixedDeltaTime);
    }

    protected virtual void LateUpdate()
    {
        animations.TickVisuals();
    }

    public void AddControlLock(object source)
    {
        if (source == null)
        {
            return;
        }

        controlLocks.Add(source);
        blackboard.controlLocked = controlLocks.Count > 0;
    }

    public void RemoveControlLock(object source)
    {
        if (source == null)
        {
            return;
        }

        controlLocks.Remove(source);
        blackboard.controlLocked = controlLocks.Count > 0;
    }

    public void ForceFacingDirection(int direction)
    {
        motor.SetFacingDirection(direction);
    }

    public void CancelAttack()
    {
        actions?.CancelAttack();
    }

    private void ResolveDependencies()
    {
        if (config == null)
        {
#if UNITY_EDITOR
            config = UnityEditor.AssetDatabase.LoadAssetAtPath<HeroConfig>("Assets/_Project/ScriptableObjects/Hero/HeroConfig.asset");
#endif
        }

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<HeroConfig>();
        }

        if (animationLibrary == null)
        {
#if UNITY_EDITOR
            animationLibrary = UnityEditor.AssetDatabase.LoadAssetAtPath<HeroAnimationLibrary>(
                "Assets/_Project/ScriptableObjects/Hero/HeroAnimationLibrary.asset");
#endif
        }

        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponent<Animator>();
        animancer = GetComponent<AnimancerComponent>();

        if (animator != null && animancer == null)
        {
            animancer = gameObject.AddComponent<AnimancerComponent>();
            animancer.Animator = animator;
        }

        if (spriteRoot == null && spriteRenderer != null)
        {
            spriteRoot = spriteRenderer.transform;
        }

        blackboard = GetOrAdd<HeroStateBlackboard>();
        inputReader = GetOrAdd<HeroInputReader>();
        sensors = GetOrAdd<HeroSensors>();
        motor = GetOrAdd<HeroMotor>();
        actions = GetOrAdd<HeroActionController>();
        animations = GetOrAdd<HeroAnimationController>();
        health = GetOrAdd<HeroHealthComponent>();
        flasher = GetComponentInChildren<SpriteFlash>(true);
    }

    private void InitializeSystems()
    {
        inputReader.Initialize(config);
        sensors.Initialize(config, blackboard, body, bodyCollider);
        motor.Initialize(config, blackboard, body, spriteRenderer, spriteRoot);
        actions.Initialize(config, blackboard, inputReader, motor);
        animations.Initialize(config, blackboard, motor, animancer, actions, animationLibrary);
        health.Initialize(config);

        health.OnDamaged += HandleDamaged;
        health.OnDeath += HandleDeath;
    }

    private void HandleDamaged(int _, Vector2 knockback)
    {
        blackboard.actorState = HeroActorState.Hurt;
        blackboard.recoiling = true;
        actions.CancelAttack();
        flasher?.FlashHit();

        Vector2 force = knockback == Vector2.zero ? DefaultKnockback() : knockback;
        motor.ApplyKnockback(force);
        motor.SetNormalMovementSuppressed(true);

        AddControlLock(this);
        StartCoroutine(HurtRecoveryRoutine());
    }

    private void HandleDeath()
    {
        blackboard.actorState = HeroActorState.Dead;
        AddControlLock(this);
        // Full fade + respawn sequence wired in Milestone 1 via GameManager.BeginSceneTransition.
        // For now, input stays locked until the scene is manually reloaded.
    }

    private IEnumerator HurtRecoveryRoutine()
    {
        yield return new WaitForSeconds(config.hurtStunDuration);
        blackboard.recoiling = false;
        motor.SetNormalMovementSuppressed(false);
        RemoveControlLock(this);
    }

    private Vector2 DefaultKnockback()
    {
        return new Vector2(-blackboard.FacingDirection * config.hurtKnockbackX, config.hurtKnockbackY);
    }

    private T GetOrAdd<T>() where T : Component
    {
        if (TryGetComponent(out T component))
        {
            return component;
        }

        return gameObject.AddComponent<T>();
    }
}
