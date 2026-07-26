using System;
using Animancer;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class UndeadExecutionerBehaviour : MonoBehaviour, IEnemyBehaviour, IBossEncounterBehaviour, IBossPresentationPhaseSource
{
    private const int ObstructionHitBufferSize = 4;

    public event Action IntroCompleted;
    public event Action DefeatPresentationCompleted;
    public event Action<int> PresentationPhaseChanged;

    [Header("Boss Configuration")]
    [SerializeField] private UndeadExecutionerConfig config;

    [Header("Foundation")]
    [SerializeField] private EnemyMotor motor;
    [SerializeField] private EnemyPerception perception;
    [SerializeField] private EnemyHealthComponent health;
    [SerializeField] private Collider2D bodyCollider;

    [Header("Presentation")]
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private GameObject presentationRoot;

    [Header("Attacks")]
    [SerializeField] private EnemyAttackController comboFirst;
    [SerializeField] private EnemyAttackController comboSecond;
    [SerializeField] private EnemyAttackController shadowBurst;
    [SerializeField] private DamageHero comboFirstDamage;
    [SerializeField] private DamageHero comboSecondDamage;
    [SerializeField] private DamageHero shadowBurstDamage;
    [SerializeField] private UndeadExecutionerSpiritPressure spiritPressure;

    [Header("Arena")]
    [SerializeField] private Transform arenaLeftLimit;
    [SerializeField] private Transform arenaRightLimit;

    private readonly RaycastHit2D[] obstructionHits = new RaycastHit2D[ObstructionHitBufferSize];

    private EnemyStateBlackboard blackboard;
    private EnemyConfig enemyConfig;
    private Rigidbody2D body;
    private AnimancerState activeAnimationState;
    private bool foundationInitialized;
    private bool prepared;
    private bool eventsSubscribed;
    private bool introSent;
    private bool defeatPresentationSent;
    private bool phaseTwo;
    private bool phaseTransitionPending;
    private bool summonEventReceived;
    private bool preferShadowBurst;
    private float stateTimer;
    private float decisionTimer;
    private float spiritTimer;
    private float authoredStartX;
    private float authoredHoverY;
    private float glideTargetX;
    private float recentHeroX;
    private bool hasRecentHeroX;

    public UndeadExecutionerState State { get; private set; } = UndeadExecutionerState.Dormant;
    public UndeadExecutionerAttack CurrentAttack { get; private set; }
    public bool IsPhaseTwo => phaseTwo;

    // Presentation-only counter, separate from the gameplay phaseTwo flag: it advances when the
    // visible phase beat begins, which is the moment worth framing.
    public int CurrentPresentationPhase { get; private set; } = 1;
    public bool IsPrepared => prepared;
    public float AuthoredHoverY => authoredHoverY;
    public UndeadExecutionerConfig Config => config;
    public EnemyMotor Motor => motor;
    public EnemyHealthComponent Health => health;
    public Collider2D BodyCollider => bodyCollider;
    public AnimancerComponent Animancer => animancer;
    public SpriteRenderer SpriteRenderer => spriteRenderer;
    public GameObject PresentationRoot => presentationRoot;
    public EnemyAttackController ComboFirst => comboFirst;
    public EnemyAttackController ComboSecond => comboSecond;
    public EnemyAttackController ShadowBurst => shadowBurst;
    public UndeadExecutionerSpiritPressure SpiritPressure => spiritPressure;
    public Transform ArenaLeftLimit => arenaLeftLimit;
    public Transform ArenaRightLimit => arenaRightLimit;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        StopEncounterWork();
        UnsubscribeEvents();
    }

    private void Update()
    {
        if (!prepared)
        {
            return;
        }

        stateTimer += Time.deltaTime;

        if (State == UndeadExecutionerState.Intro)
        {
            if (!introSent && stateTimer >= Mathf.Max(0f, config != null ? config.introDuration : 0f))
            {
                introSent = true;
                IntroCompleted?.Invoke();
            }

            return;
        }

        if (State == UndeadExecutionerState.Defeated)
        {
            if (!defeatPresentationSent
                && stateTimer >= Mathf.Max(0.1f, config != null ? config.deathFailSafeDuration : 2f))
            {
                CompleteDefeatPresentation();
            }

            return;
        }

        if (State == UndeadExecutionerState.ExecutionerCombo
            || State == UndeadExecutionerState.ShadowBurst)
        {
            float clipLength = activeAnimationState != null ? activeAnimationState.Length : 0f;
            float padding = config != null ? config.attackFailSafePadding : 0.35f;
            if (stateTimer >= Mathf.Max(0.1f, clipLength + padding))
            {
                EndCurrentAttack();
            }

            return;
        }

        if (State == UndeadExecutionerState.PhaseTransition)
        {
            float clipLength = activeAnimationState != null ? activeAnimationState.Length : 0f;
            float padding = config != null ? config.phaseTransitionFailSafePadding : 0.35f;
            if (stateTimer >= Mathf.Max(0.1f, clipLength + padding))
            {
                CompletePhaseTransition();
            }

            return;
        }

        if (State != UndeadExecutionerState.Neutral)
        {
            return;
        }

        if (blackboard != null && (blackboard.dead || blackboard.hurt || blackboard.recoiling || blackboard.attacking))
        {
            return;
        }

        Transform target = perception != null ? perception.CurrentTarget : null;
        if (target != null)
        {
            recentHeroX = target.position.x;
            hasRecentHeroX = true;
            FaceTowards(recentHeroX);
        }

        if (phaseTransitionPending)
        {
            BeginPhaseTransition();
            return;
        }

        if (phaseTwo)
        {
            spiritTimer -= Time.deltaTime;
            if (spiritTimer <= 0f && hasRecentHeroX && BeginSpiritPressure(recentHeroX))
            {
                return;
            }
        }

        decisionTimer -= Time.deltaTime;
        if (decisionTimer > 0f || target == null)
        {
            return;
        }

        EvaluateNeutralDecision(target.position.x);
    }

    private void FixedUpdate()
    {
        if (!prepared || State != UndeadExecutionerState.Repositioning || body == null || motor == null)
        {
            return;
        }

        float remaining = glideTargetX - body.position.x;
        float tolerance = config != null ? config.glideStopTolerance : 0.08f;
        if (Mathf.Abs(remaining) <= tolerance)
        {
            FinishGlide();
            return;
        }

        float direction = Mathf.Sign(remaining);
        float fixedDistance = Mathf.Abs((config != null ? config.glideSpeed : 2.5f) * Time.fixedDeltaTime);
        if (HasHorizontalObstruction(direction, fixedDistance + tolerance))
        {
            FinishGlide();
            return;
        }

        motor.MoveHorizontal(direction, config != null ? config.glideSpeed : 2.5f);

        if ((direction > 0f && body.position.x >= glideTargetX)
            || (direction < 0f && body.position.x <= glideTargetX))
        {
            FinishGlide();
        }
    }

    public void Initialize(EnemyStateBlackboard stateBlackboard, Rigidbody2D rigidbody)
    {
        blackboard = stateBlackboard;
        body = rigidbody;
        foundationInitialized = blackboard != null && body != null;
        CacheComponents();
        SubscribeEvents();
    }

    public bool TryPrepareForEncounter()
    {
        CacheComponents();
        SubscribeEvents();

        prepared = foundationInitialized && config != null && motor != null && health != null && animancer != null;
        introSent = false;
        defeatPresentationSent = false;
        CurrentPresentationPhase = 1;
        phaseTwo = false;
        phaseTransitionPending = false;
        summonEventReceived = false;
        preferShadowBurst = true;
        CurrentAttack = UndeadExecutionerAttack.None;
        hasRecentHeroX = false;
        authoredHoverY = body != null ? body.position.y : transform.position.y;
        authoredStartX = body != null ? body.position.x : transform.position.x;

        if (body != null)
        {
            body.gravityScale = 0f;
            body.constraints |= RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
        }

        if (!prepared)
        {
            StopEncounterWork();
            Debug.LogError($"[{nameof(UndeadExecutionerBehaviour)}] '{name}' is missing required foundation, config, motor, health, or Animancer references.", this);
            return false;
        }

        if (!ConfigureAttackControllers())
        {
            prepared = false;
            StopEncounterWork();
            Debug.LogError($"[{nameof(UndeadExecutionerBehaviour)}] '{name}' could not configure all authored attack controllers.", this);
            return false;
        }

        if (presentationRoot != null)
        {
            presentationRoot.SetActive(true);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        SetState(UndeadExecutionerState.Dormant);
        PlayLoop(config.idleClip);
        return true;
    }

    public void PlayIntro()
    {
        if (!prepared || State != UndeadExecutionerState.Dormant)
        {
            return;
        }

        StopHorizontal();
        SetState(UndeadExecutionerState.Intro);
        PlayLoop(config.idleClip);
    }

    public void BeginCombat()
    {
        if (!prepared || State != UndeadExecutionerState.Intro || !introSent)
        {
            return;
        }

        EnterNeutral();
    }

    public void InterruptEncounter()
    {
        if (State == UndeadExecutionerState.Completed || State == UndeadExecutionerState.Interrupted)
        {
            return;
        }

        SetState(UndeadExecutionerState.Interrupted);
        StopEncounterWork();
    }

    public void NotifyEncounterCompleted()
    {
        if (State == UndeadExecutionerState.Completed)
        {
            return;
        }

        StopEncounterWork();
        SetState(UndeadExecutionerState.Completed);
        gameObject.SetActive(false);
    }

    public void HandleComboFirstOpen()
    {
        if (State == UndeadExecutionerState.ExecutionerCombo)
        {
            comboFirst?.OpenAttackWindow();
        }
    }

    public void HandleComboFirstClose()
    {
        if (State != UndeadExecutionerState.ExecutionerCombo)
        {
            return;
        }

        comboFirst?.CloseAttackWindow();
        comboFirst?.CompleteAttack();
    }

    public void HandleComboSecondBegin()
    {
        if (State != UndeadExecutionerState.ExecutionerCombo
            || comboSecond == null
            || !comboSecond.BeginAttack())
        {
            EndCurrentAttack();
        }
    }

    public void HandleComboSecondOpen()
    {
        if (State == UndeadExecutionerState.ExecutionerCombo)
        {
            comboSecond?.OpenAttackWindow();
        }
    }

    public void HandleComboSecondClose()
    {
        if (State == UndeadExecutionerState.ExecutionerCombo)
        {
            comboSecond?.CloseAttackWindow();
        }
    }

    public void HandleComboComplete()
    {
        if (State != UndeadExecutionerState.ExecutionerCombo)
        {
            return;
        }

        comboSecond?.CompleteAttack();
        EndCurrentAttack(false);
    }

    public void HandleShadowBurstOpen()
    {
        if (State == UndeadExecutionerState.ShadowBurst)
        {
            shadowBurst?.OpenAttackWindow();
        }
    }

    public void HandleShadowBurstClose()
    {
        if (State == UndeadExecutionerState.ShadowBurst)
        {
            shadowBurst?.CloseAttackWindow();
        }
    }

    public void HandleShadowBurstComplete()
    {
        if (State != UndeadExecutionerState.ShadowBurst)
        {
            return;
        }

        shadowBurst?.CompleteAttack();
        EndCurrentAttack(false);
    }

    public void HandleSummonSpirit()
    {
        if (State != UndeadExecutionerState.PhaseTransition || summonEventReceived)
        {
            return;
        }

        summonEventReceived = true;
        spiritTimer = 0f;
    }

    private void EvaluateNeutralDecision(float heroX)
    {
        float distance = Mathf.Abs(heroX - transform.position.x);
        if (distance < config.tooCloseDistance)
        {
            if (preferShadowBurst && BeginShadowBurst())
            {
                preferShadowBurst = false;
                return;
            }

            if (!TryBeginGlide(heroX, true))
            {
                BeginShadowBurst();
            }
            else
            {
                preferShadowBurst = true;
            }

            return;
        }

        if (distance >= config.comboMinimumRange && distance <= config.comboMaximumRange)
        {
            BeginExecutionerCombo();
            return;
        }

        if (distance > config.preferredDistanceMaximum)
        {
            if (!TryBeginGlide(heroX, false))
            {
                ResetDecisionTimer();
            }

            return;
        }

        ResetDecisionTimer();
    }

    private void BeginExecutionerCombo()
    {
        if (comboFirst == null || !comboFirst.BeginAttack())
        {
            ResetDecisionTimer();
            return;
        }

        StopHorizontal();
        CurrentAttack = UndeadExecutionerAttack.ExecutionerCombo;
        SetState(UndeadExecutionerState.ExecutionerCombo);
        PlayOneShot(config.executionerComboClip, HandleComboComplete);
    }

    private bool BeginShadowBurst()
    {
        if (shadowBurst == null || !shadowBurst.BeginAttack())
        {
            ResetDecisionTimer();
            return false;
        }

        StopHorizontal();
        CurrentAttack = UndeadExecutionerAttack.ShadowBurst;
        SetState(UndeadExecutionerState.ShadowBurst);
        PlayOneShot(config.shadowBurstClip, HandleShadowBurstComplete);
        return true;
    }

    private bool TryBeginGlide(float heroX, bool escapeOverlap)
    {
        if (body == null || motor == null)
        {
            return false;
        }

        float left = GetArenaLeft();
        float right = GetArenaRight();
        float currentX = body.position.x;
        float clearance = config.arenaEdgeClearance;
        left += clearance;
        right -= clearance;

        if (right <= left)
        {
            return false;
        }

        float target;
        if (escapeOverlap)
        {
            float leftTarget = Mathf.Max(left, currentX - config.glideMaximumDistance);
            float rightTarget = Mathf.Min(right, currentX + config.glideMaximumDistance);
            leftTarget = PreventHeroCrossing(currentX, leftTarget, heroX);
            rightTarget = PreventHeroCrossing(currentX, rightTarget, heroX);

            float leftDistance = currentX - leftTarget;
            float rightDistance = rightTarget - currentX;
            float leftSpace = currentX - left;
            float rightSpace = right - currentX;

            bool leftUsable = leftDistance >= config.glideMinimumDistance
                && !HasHorizontalObstruction(-1f, leftDistance);
            bool rightUsable = rightDistance >= config.glideMinimumDistance
                && !HasHorizontalObstruction(1f, rightDistance);

            if (!leftUsable && !rightUsable)
            {
                return false;
            }

            target = rightUsable && (!leftUsable || rightSpace >= leftSpace)
                ? rightTarget
                : leftTarget;
        }
        else
        {
            float direction = Mathf.Sign(heroX - currentX);
            if (Mathf.Approximately(direction, 0f))
            {
                return false;
            }

            float desiredDistance = Mathf.Lerp(
                config.preferredDistanceMinimum,
                config.preferredDistanceMaximum,
                0.5f);
            float desiredX = heroX - direction * desiredDistance;
            float maximumTarget = currentX + direction * config.glideMaximumDistance;
            target = direction > 0f
                ? Mathf.Min(desiredX, maximumTarget)
                : Mathf.Max(desiredX, maximumTarget);
            target = Mathf.Clamp(target, left, right);
            target = PreventHeroCrossing(currentX, target, heroX);

            float travel = Mathf.Abs(target - currentX);
            if (travel < config.glideMinimumDistance || HasHorizontalObstruction(direction, travel))
            {
                return false;
            }
        }

        glideTargetX = target;
        SetState(UndeadExecutionerState.Repositioning);
        PlayLoop(phaseTwo ? config.phaseTwoIdleClip : config.idleClip);
        return true;
    }

    private float PreventHeroCrossing(float currentX, float targetX, float heroX)
    {
        float clearance = config.heroCrossingClearance;
        if (targetX > currentX && heroX > currentX && targetX >= heroX - clearance)
        {
            return Mathf.Max(currentX, heroX - clearance);
        }

        if (targetX < currentX && heroX < currentX && targetX <= heroX + clearance)
        {
            return Mathf.Min(currentX, heroX + clearance);
        }

        return targetX;
    }

    private void FinishGlide()
    {
        StopHorizontal();
        EnterNeutral();
    }

    private bool BeginSpiritPressure(float sampledHeroX)
    {
        if (spiritPressure == null || spiritPressure.IsActive)
        {
            return false;
        }

        float left = GetArenaLeft() + config.spiritWallClearance;
        float right = GetArenaRight() - config.spiritWallClearance;
        if (right <= left)
        {
            return false;
        }

        float spawnX = Mathf.Clamp(sampledHeroX, left, right);
        float clearance = config.spiritHeroClearance;
        if (Mathf.Abs(spawnX - sampledHeroX) < clearance)
        {
            float leftCandidate = sampledHeroX - clearance;
            float rightCandidate = sampledHeroX + clearance;
            float leftSpace = sampledHeroX - left;
            float rightSpace = right - sampledHeroX;

            if (rightSpace >= leftSpace && rightCandidate <= right)
            {
                spawnX = rightCandidate;
            }
            else if (leftCandidate >= left)
            {
                spawnX = leftCandidate;
            }
            else
            {
                spawnX = Mathf.Clamp(rightCandidate, left, right);
            }
        }

        StopHorizontal();
        SetState(UndeadExecutionerState.SpiritAction);
        if (!spiritPressure.Begin(spawnX, authoredHoverY + config.spiritVerticalOffset))
        {
            EnterNeutral();
            return false;
        }

        return true;
    }

    private void HandleSpiritCompleted()
    {
        if (State != UndeadExecutionerState.SpiritAction)
        {
            return;
        }

        spiritTimer = config != null ? config.spiritInterval : 5f;
        EnterNeutral();
    }

    private void BeginPhaseTransition()
    {
        if (phaseTwo || State != UndeadExecutionerState.Neutral)
        {
            return;
        }

        phaseTransitionPending = false;
        summonEventReceived = false;
        StopHorizontal();
        SetState(UndeadExecutionerState.PhaseTransition);
        CurrentPresentationPhase = 2;
        PresentationPhaseChanged?.Invoke(CurrentPresentationPhase);
        PlayOneShot(config.summonClip, CompletePhaseTransition);
    }

    private void CompletePhaseTransition()
    {
        if (State != UndeadExecutionerState.PhaseTransition)
        {
            return;
        }

        phaseTwo = true;
        spiritTimer = 0f;
        EnterNeutral();
    }

    private void HandleHealthChanged(int current, int maximum)
    {
        if (config == null || current <= 0 || maximum <= 0 || phaseTwo || phaseTransitionPending)
        {
            return;
        }

        int threshold = Mathf.CeilToInt(maximum * config.phaseTwoHealthRatio);
        if (current <= threshold)
        {
            phaseTransitionPending = true;
        }
    }

    private void HandleDeath()
    {
        if (State == UndeadExecutionerState.Defeated || State == UndeadExecutionerState.Completed)
        {
            return;
        }

        prepared = true;
        CurrentAttack = UndeadExecutionerAttack.None;
        phaseTransitionPending = false;
        StopHorizontal();
        InterruptAttacks();
        spiritPressure?.Interrupt();
        SetState(UndeadExecutionerState.Defeated);
        PlayOneShot(config != null ? config.deathClip : null, CompleteDefeatPresentation);
    }

    private void CompleteDefeatPresentation()
    {
        if (State != UndeadExecutionerState.Defeated || defeatPresentationSent)
        {
            return;
        }

        defeatPresentationSent = true;
        ClearActiveEndEvent();
        DefeatPresentationCompleted?.Invoke();
    }

    private void EndCurrentAttack(bool interrupted = true)
    {
        if (State != UndeadExecutionerState.ExecutionerCombo
            && State != UndeadExecutionerState.ShadowBurst)
        {
            return;
        }

        if (interrupted)
        {
            InterruptAttacks();
        }

        CurrentAttack = UndeadExecutionerAttack.None;
        EnterNeutral();
    }

    private void EnterNeutral()
    {
        if (!prepared || (blackboard != null && blackboard.dead))
        {
            return;
        }

        StopHorizontal();
        CurrentAttack = UndeadExecutionerAttack.None;
        SetState(UndeadExecutionerState.Neutral);
        ResetDecisionTimer();
        PlayLoop(phaseTwo ? config.phaseTwoIdleClip : config.idleClip);
    }

    private void ResetDecisionTimer()
    {
        decisionTimer = config != null ? config.GetDecisionDelay(phaseTwo) : 0.5f;
    }

    private bool ConfigureAttackControllers()
    {
        if (comboFirst == null || comboSecond == null || shadowBurst == null)
        {
            return false;
        }

        bool configured = ConfigureAttack(comboFirst, config.comboFirstTiming)
            && ConfigureAttack(comboSecond, config.comboSecondTiming)
            && ConfigureAttack(shadowBurst, config.shadowBurstTiming)
            && (spiritPressure == null || spiritPressure.Configure(config));

        if (comboFirstDamage != null)
        {
            comboFirstDamage.damageDealt = config.comboDamage;
        }

        if (comboSecondDamage != null)
        {
            comboSecondDamage.damageDealt = config.comboDamage;
        }

        if (shadowBurstDamage != null)
        {
            shadowBurstDamage.damageDealt = config.shadowBurstDamage;
        }

        return configured;
    }

    private static bool ConfigureAttack(
        EnemyAttackController controller,
        UndeadExecutionerConfig.AttackTiming timing)
    {
        return controller.TryConfigureTimings(
            timing.startup,
            timing.active,
            timing.recovery,
            timing.cooldown);
    }

    private void CacheComponents()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (motor == null) motor = GetComponent<EnemyMotor>();
        if (perception == null) perception = GetComponent<EnemyPerception>();
        if (health == null) health = GetComponent<EnemyHealthComponent>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
        if (animancer == null) animancer = GetComponentInChildren<AnimancerComponent>(true);
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (presentationRoot == null && spriteRenderer != null) presentationRoot = spriteRenderer.gameObject;

        EnemyController enemyController = GetComponent<EnemyController>();
        if (enemyController != null)
        {
            enemyConfig = enemyController.Config;
        }
    }

    private void SubscribeEvents()
    {
        if (eventsSubscribed || health == null)
        {
            return;
        }

        health.OnHealthChanged += HandleHealthChanged;
        health.OnDeath += HandleDeath;
        if (spiritPressure != null)
        {
            spiritPressure.Completed += HandleSpiritCompleted;
        }

        eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed)
        {
            return;
        }

        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnDeath -= HandleDeath;
        }

        if (spiritPressure != null)
        {
            spiritPressure.Completed -= HandleSpiritCompleted;
        }

        eventsSubscribed = false;
    }

    private void FaceTowards(float worldX)
    {
        if (motor == null)
        {
            return;
        }

        float direction = worldX - transform.position.x;
        if (Mathf.Abs(direction) <= 0.01f)
        {
            return;
        }

        float desiredFacing = Mathf.Sign(direction);
        if (!Mathf.Approximately(desiredFacing, motor.FacingDirection))
        {
            motor.Flip();
        }
    }

    private bool HasHorizontalObstruction(float direction, float distance)
    {
        if (bodyCollider == null || enemyConfig == null || enemyConfig.terrainLayers.value == 0)
        {
            return false;
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 size = new Vector2(
            Mathf.Max(0.05f, bounds.size.x * 0.9f),
            Mathf.Max(0.05f, bounds.size.y * 0.9f));
        ContactFilter2D terrainFilter = new ContactFilter2D();
        terrainFilter.SetLayerMask(enemyConfig.terrainLayers);
        terrainFilter.useTriggers = false;
        int hitCount = Physics2D.BoxCast(
            bounds.center,
            size,
            0f,
            new Vector2(Mathf.Sign(direction), 0f),
            terrainFilter,
            obstructionHits,
            Mathf.Max(0f, distance));

        return hitCount > 0;
    }

    private float GetArenaLeft()
    {
        if (arenaLeftLimit != null)
        {
            return arenaLeftLimit.position.x;
        }

        float fallback = config != null ? config.glideMaximumDistance * 2f : 8f;
        return authoredStartX - fallback;
    }

    private float GetArenaRight()
    {
        if (arenaRightLimit != null)
        {
            return arenaRightLimit.position.x;
        }

        float fallback = config != null ? config.glideMaximumDistance * 2f : 8f;
        return authoredStartX + fallback;
    }

    private void StopEncounterWork()
    {
        StopAllCoroutines();
        StopHorizontal();
        InterruptAttacks();
        spiritPressure?.Interrupt();
        ClearActiveEndEvent();
        CurrentAttack = UndeadExecutionerAttack.None;
    }

    private void InterruptAttacks()
    {
        comboFirst?.InterruptAttack(false);
        comboSecond?.InterruptAttack(false);
        shadowBurst?.InterruptAttack(false);
    }

    private void StopHorizontal()
    {
        motor?.StopHorizontal();
    }

    private void PlayLoop(AnimationClip clip)
    {
        ClearActiveEndEvent();
        if (animancer != null && clip != null)
        {
            activeAnimationState = animancer.Play(
                clip,
                config != null ? config.animationFadeDuration : 0.05f,
                FadeMode.FromStart);
        }
    }

    private void PlayOneShot(AnimationClip clip, Action onEnd)
    {
        ClearActiveEndEvent();
        if (animancer == null || clip == null)
        {
            onEnd?.Invoke();
            return;
        }

        activeAnimationState = animancer.Play(
            clip,
            config != null ? config.animationFadeDuration : 0.05f,
            FadeMode.FromStart);
        activeAnimationState.Events(this).OnEnd = () =>
        {
            ClearActiveEndEvent();
            onEnd?.Invoke();
        };
    }

    private void ClearActiveEndEvent()
    {
        if (activeAnimationState == null)
        {
            return;
        }

        activeAnimationState.Events(this).OnEnd = null;
        activeAnimationState = null;
    }

    private void SetState(UndeadExecutionerState next)
    {
        State = next;
        stateTimer = 0f;
    }

    // Editor-only authoring aid: arena span, hover height, and (when a config is assigned)
    // distance-threshold guidance for the neutral decision policy. Never called outside the
    // Scene view.
    private void OnDrawGizmosSelected()
    {
        float hoverY = prepared ? authoredHoverY : transform.position.y;
        float centerX = transform.position.x;

        if (arenaLeftLimit != null || arenaRightLimit != null)
        {
            float left = arenaLeftLimit != null ? arenaLeftLimit.position.x : centerX - 4f;
            float right = arenaRightLimit != null ? arenaRightLimit.position.x : centerX + 4f;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(left, hoverY - 1.5f, 0f), new Vector3(left, hoverY + 1.5f, 0f));
            Gizmos.DrawLine(new Vector3(right, hoverY - 1.5f, 0f), new Vector3(right, hoverY + 1.5f, 0f));

            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawLine(new Vector3(left, hoverY, 0f), new Vector3(right, hoverY, 0f));
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(centerX - 0.6f, hoverY, 0f), new Vector3(centerX + 0.6f, hoverY, 0f));

        if (config != null)
        {
            DrawRangeTick(centerX, hoverY, config.tooCloseDistance, new Color(1f, 0.2f, 0.2f, 0.8f));
            DrawRangeTick(centerX, hoverY, config.comboMinimumRange, new Color(1f, 0.6f, 0f, 0.8f));
            DrawRangeTick(centerX, hoverY, config.comboMaximumRange, new Color(1f, 0.6f, 0f, 0.8f));
            DrawRangeTick(centerX, hoverY, config.preferredDistanceMinimum, new Color(0.2f, 1f, 0.4f, 0.6f));
            DrawRangeTick(centerX, hoverY, config.preferredDistanceMaximum, new Color(0.2f, 1f, 0.4f, 0.6f));
        }
    }

    private static void DrawRangeTick(float centerX, float hoverY, float distance, Color color)
    {
        if (distance <= 0f)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawLine(new Vector3(centerX - distance, hoverY - 0.35f, 0f), new Vector3(centerX - distance, hoverY + 0.35f, 0f));
        Gizmos.DrawLine(new Vector3(centerX + distance, hoverY - 0.35f, 0f), new Vector3(centerX + distance, hoverY + 0.35f, 0f));
    }
}
