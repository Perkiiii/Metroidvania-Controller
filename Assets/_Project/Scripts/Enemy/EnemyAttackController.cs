using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAttackController : MonoBehaviour
{
    public event Action OnAttackStarted;
    public event Action OnAttackWindowOpened;
    public event Action OnAttackWindowClosed;
    public event Action OnAttackCompleted;
    public event Action OnAttackInterrupted;

    [Header("Timing")]
    [SerializeField] private float startupDuration = 0.35f;
    [SerializeField] private float activeDuration = 0.18f;
    [SerializeField] private float recoveryDuration = 0.35f;
    [SerializeField] private float cooldownDuration = -1f;

    [Header("Hitboxes")]
    [SerializeField] private EnemyAttackHitbox[] hitboxes;
    [SerializeField] private LayerMask heroHurtboxLayers;

    [Header("Interrupts")]
    [SerializeField] private bool interruptOnHurt = true;
    [SerializeField] private bool interruptOnDeath = true;

    private readonly HashSet<HeroBox> damagedHeroes = new HashSet<HeroBox>();

    private EnemyConfig config;
    private EnemyStateBlackboard blackboard;
    private EnemyMotor motor;
    private EnemyHealthComponent health;
    private EnemyAttackPhase phase = EnemyAttackPhase.Idle;
    private ContactFilter2D heroFilter;
    private float phaseTimer;
    private float cooldownRemaining;
    private bool initialized;

    public EnemyAttackPhase Phase => phase;
    public bool IsAttacking => phase == EnemyAttackPhase.Startup || phase == EnemyAttackPhase.Active || phase == EnemyAttackPhase.Recovery;
    public bool IsAttackWindowActive => phase == EnemyAttackPhase.Active;
    public bool IsOnCooldown => cooldownRemaining > 0f || phase == EnemyAttackPhase.Cooldown;
    public bool CanStartAttack => initialized
        && phase == EnemyAttackPhase.Idle
        && cooldownRemaining <= 0f
        && blackboard != null
        && !blackboard.dead
        && !blackboard.hurt
        && !blackboard.recoiling
        && !blackboard.attacking;

    public void Initialize(EnemyConfig enemyConfig, EnemyStateBlackboard stateBlackboard, EnemyMotor enemyMotor)
    {
        config = enemyConfig;
        blackboard = stateBlackboard;
        motor = enemyMotor;
        CacheHitboxes();
        BuildHeroFilter();
        SubscribeToHealth();
        CloseAllHitboxes();
        initialized = true;
    }

    private void Awake()
    {
        CacheHitboxes();
        CloseAllHitboxes();
    }

    private void OnDestroy()
    {
        UnsubscribeFromHealth();
    }

    private void OnDisable()
    {
        InterruptAttack(false);
        cooldownRemaining = 0f;
        SetPhase(EnemyAttackPhase.Idle);
    }

    private void Update()
    {
        if (blackboard != null && blackboard.dead)
        {
            InterruptAttack(false);
            return;
        }

        if (cooldownRemaining > 0f)
        {
            cooldownRemaining -= Time.deltaTime;
            if (cooldownRemaining <= 0f && phase == EnemyAttackPhase.Cooldown)
            {
                SetPhase(EnemyAttackPhase.Idle);
            }
        }

        if (!IsAttacking)
        {
            return;
        }

        phaseTimer += Time.deltaTime;
        if (phase == EnemyAttackPhase.Startup && phaseTimer >= Mathf.Max(0f, startupDuration))
        {
            OpenAttackWindow();
        }
        else if (phase == EnemyAttackPhase.Active && phaseTimer >= Mathf.Max(0f, activeDuration))
        {
            CloseAttackWindow();
        }
        else if (phase == EnemyAttackPhase.Recovery && phaseTimer >= Mathf.Max(0f, recoveryDuration))
        {
            CompleteAttack();
        }
    }

    private void FixedUpdate()
    {
        if (phase != EnemyAttackPhase.Active || hitboxes == null)
        {
            return;
        }

        for (int i = 0; i < hitboxes.Length; i++)
        {
            hitboxes[i]?.EvaluateActiveWindow(heroFilter, damagedHeroes, this);
        }
    }

    public bool BeginAttack()
    {
        if (!CanStartAttack)
        {
            return false;
        }

        motor?.StopHorizontal();
        damagedHeroes.Clear();
        CloseAllHitboxes();
        SetPhase(EnemyAttackPhase.Startup);
        OnAttackStarted?.Invoke();

        if (startupDuration <= 0f)
        {
            OpenAttackWindow();
        }

        return true;
    }

    public bool TryConfigureTimings(float startup, float active, float recovery, float cooldown)
    {
        if (IsAttacking
            || float.IsNaN(startup) || float.IsInfinity(startup) || startup < 0f
            || float.IsNaN(active) || float.IsInfinity(active) || active < 0f
            || float.IsNaN(recovery) || float.IsInfinity(recovery) || recovery < 0f
            || float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown < -1f)
        {
            return false;
        }

        startupDuration = startup;
        activeDuration = active;
        recoveryDuration = recovery;
        cooldownDuration = cooldown;
        return true;
    }

    public void OpenAttackWindow()
    {
        if (phase == EnemyAttackPhase.Active)
        {
            return;
        }

        if (phase != EnemyAttackPhase.Startup)
        {
            return;
        }

        damagedHeroes.Clear();
        SetPhase(EnemyAttackPhase.Active);
        SetHitboxesActive(true);
        OnAttackWindowOpened?.Invoke();

        if (activeDuration <= 0f)
        {
            CloseAttackWindow();
        }
    }

    public void CloseAttackWindow()
    {
        if (phase != EnemyAttackPhase.Active)
        {
            CloseAllHitboxes();
            return;
        }

        CloseAllHitboxes();
        OnAttackWindowClosed?.Invoke();
        SetPhase(EnemyAttackPhase.Recovery);

        if (recoveryDuration <= 0f)
        {
            CompleteAttack();
        }
    }

    public void CompleteAttack()
    {
        if (!IsAttacking)
        {
            return;
        }

        CloseAllHitboxes();
        float cooldown = cooldownDuration >= 0f ? cooldownDuration : (config != null ? config.attackCooldown : 0f);
        cooldownRemaining = Mathf.Max(0f, cooldown);
        SetPhase(cooldownRemaining > 0f ? EnemyAttackPhase.Cooldown : EnemyAttackPhase.Idle);
        OnAttackCompleted?.Invoke();
    }

    public void InterruptAttack()
    {
        InterruptAttack(true);
    }

    public void InterruptAttack(bool notify)
    {
        bool wasAttacking = IsAttacking || phase == EnemyAttackPhase.Cooldown;
        CloseAllHitboxes();
        damagedHeroes.Clear();
        cooldownRemaining = 0f;
        SetPhase(EnemyAttackPhase.Idle);

        if (notify && wasAttacking)
        {
            OnAttackInterrupted?.Invoke();
        }
    }

    private void SetPhase(EnemyAttackPhase nextPhase)
    {
        phase = nextPhase;
        phaseTimer = 0f;

        if (blackboard != null)
        {
            blackboard.attacking = IsAttacking;
            blackboard.attackWindowActive = phase == EnemyAttackPhase.Active;
        }
    }

    private void SetHitboxesActive(bool active)
    {
        if (hitboxes == null)
        {
            return;
        }

        for (int i = 0; i < hitboxes.Length; i++)
        {
            hitboxes[i]?.SetWindowActive(active);
        }
    }

    private void CloseAllHitboxes()
    {
        SetHitboxesActive(false);

        if (blackboard != null)
        {
            blackboard.attackWindowActive = false;
        }
    }

    private void CacheHitboxes()
    {
        if (hitboxes == null || hitboxes.Length == 0)
        {
            hitboxes = GetComponentsInChildren<EnemyAttackHitbox>(true);
        }
    }

    private void BuildHeroFilter()
    {
        heroFilter = new ContactFilter2D();
        heroFilter.useTriggers = true;

        LayerMask targetLayers = heroHurtboxLayers;
        if (targetLayers.value == 0)
        {
            targetLayers = new LayerMask { value = Physics2D.AllLayers };
        }

        heroFilter.SetLayerMask(targetLayers);
    }

    private void SubscribeToHealth()
    {
        EnemyHealthComponent nextHealth = GetComponentInParent<EnemyHealthComponent>();
        if (health == nextHealth)
        {
            return;
        }

        UnsubscribeFromHealth();
        health = nextHealth;

        if (health == null)
        {
            return;
        }

        health.OnDamaged += HandleDamaged;
        health.OnDeath += HandleDeath;
    }

    private void UnsubscribeFromHealth()
    {
        if (health == null)
        {
            return;
        }

        health.OnDamaged -= HandleDamaged;
        health.OnDeath -= HandleDeath;
        health = null;
    }

    private void HandleDamaged()
    {
        if (interruptOnHurt)
        {
            InterruptAttack();
        }
    }

    private void HandleDeath()
    {
        if (interruptOnDeath)
        {
            InterruptAttack(false);
        }
    }
}
