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
    [SerializeField] private HeroAbilityConfig abilityConfig;
    [SerializeField] private HeroAnimationLibrary animationLibrary;
    [SerializeField] private PlayerAbilityState abilityState;
    [SerializeField] private Transform spriteRoot;

    private readonly HashSet<object> controlLocks = new HashSet<object>();

    private Coroutine hurtRoutine;
    private Coroutine deathFallbackRoutine;
    private bool respawnTriggered;

    private HeroStateBlackboard blackboard;
    private HeroInputReader inputReader;
    private HeroSensors sensors;
    private HeroMotor motor;
    private HeroActionController actions;
    private HeroAnimationController animations;
    private HeroHealthComponent health;
    private HeroCameraSignalBridge cameraSignals;
    private HeroAudioController audioController;
    private HeroSceneEntry sceneEntry;
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
        cameraSignals.Tick();
        actions.Tick();
        audioController.Tick();
    }

    protected virtual void FixedUpdate()
    {
        float fixedDeltaTime = Time.fixedDeltaTime;
        sensors.FixedTick();
        CheckLanding();
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

    public void BeginSceneEntryPlacement(TransitionPoint destinationGate)
    {
        if (sceneEntry != null) sceneEntry.PrepareSceneEntry(destinationGate);
    }

    public void BeginSceneEntryMotion(TransitionPoint destinationGate)
    {
        if (sceneEntry != null) sceneEntry.PlaySceneEntryMotion(destinationGate);
    }

    public bool IsEnteringScene => sceneEntry != null && sceneEntry.IsEnteringScene;

    public void PushOutOfGate(Vector2 worldOffset, bool zeroVelocityX, bool zeroVelocityY)
    {
        if (motor != null) motor.PushOut(worldOffset, zeroVelocityX, zeroVelocityY);
    }

    public void TeleportForScenePlacement(Vector3 position)
    {
        if (motor != null)
        {
            motor.TeleportTo(position);
            return;
        }

        transform.position = position;
    }

    public bool IsRecoiling => blackboard != null && blackboard.recoiling;
    public bool IsControlLocked => blackboard != null && (blackboard.controlLocked || blackboard.inputBlocked);

    public void ResetAfterRespawn()
    {
        ResetTransientHeroState();
        controlLocks.Clear();
        blackboard.controlLocked = false;
        respawnTriggered = false;
    }

    public void ResetAfterHazardRecovery()
    {
        ResetTransientHeroState();
        RemoveControlLock(this);
        respawnTriggered = false;
    }

    private void ResetTransientHeroState()
    {
        if (hurtRoutine != null)
        {
            StopCoroutine(hurtRoutine);
            hurtRoutine = null;
        }

        if (deathFallbackRoutine != null)
        {
            StopCoroutine(deathFallbackRoutine);
            deathFallbackRoutine = null;
        }

        actions.CancelAttack();
        motor.ResetMotion();
        motor.SetNormalMovementSuppressed(false);
        body.bodyType = RigidbodyType2D.Dynamic;
        bodyCollider.enabled = true;

        blackboard.actorState = HeroActorState.Airborne;
        blackboard.recoiling = false;
        blackboard.dashing = false;
        blackboard.attacking = false;
        blackboard.attackRecovering = false;
        blackboard.upAttacking = false;
        blackboard.downAttacking = false;
        blackboard.wallSliding = false;
        blackboard.wallJumping = false;
        blackboard.jumping = false;
        blackboard.jumpSustaining = false;
        blackboard.rising = false;
        blackboard.falling = false;
        blackboard.moving = false;
        blackboard.desiredMoveX = 0f;
        blackboard.velocity = Vector2.zero;
    }

    private void ResolveDependencies()
    {
        if (config == null)
        {
            Debug.LogError("[HeroController] HeroConfig is not assigned. Assign Assets/_Project/ScriptableObjects/Hero/HeroConfig.asset on the Hero prefab.", this);
        }

        if (animationLibrary == null)
        {
            Debug.LogError("[HeroController] HeroAnimationLibrary is not assigned. Assign Assets/_Project/ScriptableObjects/Hero/HeroAnimationLibrary.asset on the Hero prefab.", this);
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
        cameraSignals = GetOrAdd<HeroCameraSignalBridge>();
        audioController = GetOrAdd<HeroAudioController>();
        sceneEntry = GetOrAdd<HeroSceneEntry>();
        flasher = GetComponentInChildren<SpriteFlash>(true);
    }

    private void InitializeSystems()
    {
        if (abilityConfig == null)
        {
            Debug.LogError("[HeroController] HeroAbilityConfig is not assigned. Gated traversal abilities (dash, wall-slide, wall-jump, double-jump) will be disabled.", this);
        }

        inputReader.Initialize(config);
        sensors.Initialize(config, blackboard, body, bodyCollider);
        motor.Initialize(config, abilityConfig, blackboard, body, bodyCollider, spriteRenderer, spriteRoot);
        audioController.Initialize(config, blackboard);
        actions.Initialize(config, abilityConfig, blackboard, inputReader, motor, audioController, abilityState);
        animations.Initialize(config, blackboard, motor, animancer, actions, animationLibrary);
        health.Initialize(config);
        cameraSignals.Initialize(blackboard, inputReader);
        sceneEntry.Initialize(this, motor, blackboard, config, health);

        health.OnDamaged += HandleDamaged;
        health.OnHazardDamaged += HandleHazardDamaged;
        health.OnDeath += HandleDeath;
        animations.DeathAnimationComplete += OnDeathAnimationComplete;
    }

    private void CheckLanding()
    {
        if (blackboard.grounded && !blackboard.wasGrounded)
        {
            audioController.PlayLand();
        }
    }

    private void HandleDamaged(int _, Vector2 knockback)
    {
        blackboard.actorState = HeroActorState.Hurt;
        blackboard.recoiling = true;
        actions.CancelAttack();
        flasher?.FlashHit();
        audioController.PlayTakeDamage();
        CameraShakeRequester.ShakeHit();

        Vector2 force = knockback == Vector2.zero ? DefaultKnockback() : knockback;
        motor.ApplyKnockback(force);
        motor.SetNormalMovementSuppressed(true);

        AddControlLock(this);
        if (hurtRoutine != null) StopCoroutine(hurtRoutine);
        hurtRoutine = StartCoroutine(HurtRecoveryRoutine());
    }

    private void HandleHazardDamaged(DamageResult _)
    {
        blackboard.actorState = HeroActorState.Hurt;
        actions.CancelAttack();
        flasher?.FlashHit();
        audioController.PlayTakeDamage();
        CameraShakeRequester.ShakeHit();
    }

    private void HandleDeath()
    {
        blackboard.actorState = HeroActorState.Dead;
        AddControlLock(this);
        body.linearVelocity = Vector2.zero;
        body.bodyType = RigidbodyType2D.Kinematic;
        bodyCollider.enabled = false;
        audioController.PlayDeath();

        respawnTriggered = false;
        deathFallbackRoutine = StartCoroutine(DeathFallbackRoutine());
        // Respawn is triggered by OnDeathAnimationComplete (Animancer end event on death clip).
        // DeathFallbackRoutine fires if the end event is missed or the clip is missing.
    }

    private void OnDeathAnimationComplete()
    {
        TriggerRespawn();
    }

    private void TriggerRespawn()
    {
        if (respawnTriggered) return;
        respawnTriggered = true;

        if (deathFallbackRoutine != null)
        {
            StopCoroutine(deathFallbackRoutine);
            deathFallbackRoutine = null;
        }

        if (GameManager.Instance != null) GameManager.Instance.BeginRespawnSequence();
    }

    private IEnumerator DeathFallbackRoutine()
    {
        yield return new WaitForSecondsRealtime(config.deathRespawnFallbackDelay);
        Debug.LogWarning("[HeroController] Death fallback triggered — animation end event may have been missed.");
        TriggerRespawn();
        deathFallbackRoutine = null;
    }

    private IEnumerator HurtRecoveryRoutine()
    {
        yield return new WaitForSeconds(config.hurtStunDuration);
        blackboard.recoiling = false;
        blackboard.actorState = HeroActorState.Airborne;
        motor.SetNormalMovementSuppressed(false);
        RemoveControlLock(this);
        hurtRoutine = null;
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
