using Animancer;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class MushroomEnemy : MonoBehaviour, IEnemyBehaviour
{
    private enum MushroomState
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Hurt,
        Dead
    }

    [Header("Movement")]
    [SerializeField] private float speed = 1.5f;
    // wallCheck mirrors with localScale.x and acts as the lower side probe.
    // groundCheck stays centered below the body; the ledge probe derives its X from wallCheck.
    [SerializeField] private Transform wallCheck;
    [FormerlySerializedAs("edgeCheck")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float wallCheckDistance = 0.08f;
    [FormerlySerializedAs("edgeCheckDistance")]
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private LayerMask terrainMask;

    [Header("Animation")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip walkClip;
    [FormerlySerializedAs("attackStartupClip")]
    [SerializeField] private AnimationClip attackClip;
    [SerializeField] private AnimationClip hitClip;
    [SerializeField] private AnimationClip dieClip;
    [SerializeField] private Vector2 deathVisualOffset = Vector2.zero;

    private Rigidbody2D body;
    private EnemyStateBlackboard blackboard;
    private AnimancerComponent animancer;
    private EnemyHealthComponent health;
    private EnemyMotor motor;
    private EnemyPerception perception;
    private EnemyRecoil recoil;
    private EnemyAttackController attackController;
    private EnemyConfig config;

    private MushroomState currentState = MushroomState.Patrol;
    private float stateTimer;
    private int facing = 1;
    private bool deathVisualOffsetApplied;
    private bool isChaseTimeoutActive;
    private float chaseLostTimer;

    public void Initialize(EnemyStateBlackboard stateBlackboard, Rigidbody2D rb)
    {
        blackboard = stateBlackboard;
        body = rb;

        EnsureComponentsCached();
        SubscribeToEvents();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animancer = GetComponent<AnimancerComponent>();
        EnsureComponentsCached();
    }

    private void Start()
    {
        if (body == null)
        {
            Debug.LogError($"MushroomEnemy on '{name}' has no Rigidbody2D. Movement disabled.", this);
            return;
        }

        // Initialize state
        EnterState(MushroomState.Patrol);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void Update()
    {
        if (blackboard != null && blackboard.dead)
        {
            if (currentState != MushroomState.Dead)
            {
                EnterState(MushroomState.Dead);
            }
            return;
        }

        stateTimer += Time.deltaTime;

        if (currentState == MushroomState.Idle)
        {
            float idleTime = (config != null) ? config.patrolIdleTime : 1.5f;
            if (stateTimer >= idleTime)
            {
                FlipFacing();
                EnterState(MushroomState.Patrol);
            }
        }
        else if (currentState == MushroomState.Hurt)
        {
            // Fallback: exit Hurt if EnemyRecoil never fires OnRecoilEnded
            // (e.g. component absent or stunDuration is 0).
            float stunFallback = (config != null) ? Mathf.Max(config.stunDuration + 0.1f, 0.5f) : 1.0f;
            if (stateTimer >= stunFallback)
            {
                HandleRecoilEnded();
            }
        }
        else if (currentState == MushroomState.Chase && isChaseTimeoutActive)
        {
            chaseLostTimer += Time.deltaTime;
            float timeout = (config != null) ? config.chaseTimeout : 3.0f;
            if (chaseLostTimer >= timeout)
            {
                EnterState(MushroomState.Patrol);
            }
        }
    }

    private void FixedUpdate()
    {
        if (IsMovementSuppressed())
        {
            return;
        }

        if (currentState == MushroomState.Patrol)
        {
            bool hitBoundary = HasReachedPatrolBoundary();
            if (hitBoundary)
            {
                EnterState(MushroomState.Idle);
            }
            else
            {
                float moveSpeed = (config != null) ? config.patrolSpeed : speed;
                MoveHorizontal(motor != null ? motor.FacingDirection : facing, moveSpeed);
            }
        }
        else if (currentState == MushroomState.Chase)
        {
            TickChaseMovement();
        }
    }

    private void TickChaseMovement()
    {
        if (perception == null)
        {
            EnterState(MushroomState.Patrol);
            return;
        }

        Vector2 targetPosition = perception.IsHeroDetected && perception.CurrentTarget != null
            ? perception.CurrentTarget.position
            : perception.LastKnownHeroPosition;

        float distanceX = targetPosition.x - transform.position.x;
        float absDistanceX = Mathf.Abs(distanceX);
        float attackRange = config != null ? config.attackRange : 1.2f;

        if (perception.IsHeroDetected && absDistanceX <= attackRange)
        {
            StopHorizontal();
            FaceDirection(distanceX);

            if (attackController != null && attackController.CanStartAttack)
            {
                EnterState(MushroomState.Attack);
                attackController.BeginAttack();
            }
            return;
        }

        if (!perception.IsHeroDetected && absDistanceX < 0.3f)
        {
            EnterState(MushroomState.Idle);
            return;
        }

        if (HasReachedPatrolBoundary())
        {
            EnterState(MushroomState.Idle);
            return;
        }

        MoveHorizontal(distanceX, config != null ? config.chaseSpeed : speed);
    }

    private void EnterState(MushroomState newState)
    {
        currentState = newState;
        stateTimer = 0f;

        switch (currentState)
        {
            case MushroomState.Idle:
                StopHorizontal();
                PlayClip(idleClip);
                break;

            case MushroomState.Patrol:
                PlayClip(walkClip);
                break;

            case MushroomState.Chase:
                PlayClip(walkClip);
                isChaseTimeoutActive = false;
                break;

            case MushroomState.Attack:
                StopHorizontal();
                PlayClip(attackClip);
                break;

            case MushroomState.Hurt:
                attackController?.InterruptAttack();
                StopHorizontal();
                PlayClip(hitClip);
                break;

            case MushroomState.Dead:
                attackController?.InterruptAttack(false);
                StopHorizontal();
                ApplyDeathVisualOffset();
                PlayClip(dieClip);
                break;
        }
    }

    private void EnsureComponentsCached()
    {
        if (motor == null) motor = GetComponent<EnemyMotor>();
        if (perception == null) perception = GetComponent<EnemyPerception>();
        if (recoil == null) recoil = GetComponent<EnemyRecoil>();
        if (attackController == null) attackController = GetComponent<EnemyAttackController>();

        EnemyController controller = GetComponent<EnemyController>();
        if (controller != null)
        {
            config = controller.Config;
        }
    }

    private void SubscribeToEvents()
    {
        health = GetComponent<EnemyHealthComponent>();
        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
            health.OnDeath += HandleDeath;
        }

        if (recoil != null)
        {
            recoil.OnRecoilEnded += HandleRecoilEnded;
        }

        if (perception != null)
        {
            perception.HeroDetected += HandleHeroDetected;
            perception.HeroLost += HandleHeroLost;
        }

        if (attackController != null)
        {
            attackController.OnAttackCompleted += HandleAttackCompleted;
            attackController.OnAttackInterrupted += HandleAttackInterrupted;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDeath -= HandleDeath;
        }

        if (recoil != null)
        {
            recoil.OnRecoilEnded -= HandleRecoilEnded;
        }

        if (perception != null)
        {
            perception.HeroDetected -= HandleHeroDetected;
            perception.HeroLost -= HandleHeroLost;
        }

        if (attackController != null)
        {
            attackController.OnAttackCompleted -= HandleAttackCompleted;
            attackController.OnAttackInterrupted -= HandleAttackInterrupted;
        }
    }

    private void HandleDamaged()
    {
        if (currentState != MushroomState.Dead)
        {
            EnterState(MushroomState.Hurt);
        }
    }

    private void HandleDeath()
    {
        EnterState(MushroomState.Dead);
    }

    private void HandleRecoilEnded()
    {
        if (currentState == MushroomState.Dead || (blackboard != null && blackboard.dead)) return;

        if (perception != null && perception.IsHeroDetected)
        {
            EnterState(MushroomState.Chase);
        }
        else
        {
            EnterState(MushroomState.Patrol);
        }
    }

    private void HandleHeroDetected(Transform target)
    {
        if (currentState == MushroomState.Idle || currentState == MushroomState.Patrol || (currentState == MushroomState.Chase && isChaseTimeoutActive))
        {
            EnterState(MushroomState.Chase);
        }
    }

    private void HandleHeroLost()
    {
        if (currentState == MushroomState.Chase)
        {
            isChaseTimeoutActive = true;
            chaseLostTimer = 0f;
        }
    }

    private void HandleAttackCompleted()
    {
        if (currentState == MushroomState.Attack)
        {
            EnterState(perception != null && perception.IsHeroDetected ? MushroomState.Chase : MushroomState.Patrol);
        }
    }

    private void HandleAttackInterrupted()
    {
        if (currentState == MushroomState.Attack)
        {
            EnterState(blackboard != null && blackboard.dead ? MushroomState.Dead : MushroomState.Hurt);
        }
    }

    private bool IsMovementSuppressed()
    {
        if (blackboard != null && (blackboard.dead || blackboard.hurt || blackboard.recoiling || blackboard.attacking))
        {
            return true;
        }

        return motor != null && motor.ExternalVelocityActive;
    }

    private void MoveHorizontal(float direction, float moveSpeed)
    {
        if (Mathf.Abs(direction) <= 0.01f)
        {
            StopHorizontal();
            return;
        }

        float normalizedDirection = Mathf.Sign(direction);
        if (motor != null)
        {
            motor.MoveHorizontal(normalizedDirection, moveSpeed);
            facing = (int)motor.FacingDirection;
        }
        else if (body != null)
        {
            facing = (int)normalizedDirection;
            FaceDirection(normalizedDirection);
            body.linearVelocity = new Vector2(normalizedDirection * moveSpeed, body.linearVelocity.y);
        }
    }

    private void StopHorizontal()
    {
        if (motor != null)
        {
            motor.StopHorizontal();
        }
        else if (body != null)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }
    }

    private void FaceDirection(float direction)
    {
        if (Mathf.Abs(direction) <= 0.01f)
        {
            return;
        }

        float normalizedDirection = Mathf.Sign(direction);
        if (motor != null)
        {
            if (!Mathf.Approximately(normalizedDirection, motor.FacingDirection))
            {
                motor.Flip();
            }

            facing = (int)motor.FacingDirection;
            return;
        }

        facing = (int)normalizedDirection;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        transform.localScale = scale;
    }

    private void FlipFacing()
    {
        if (motor != null)
        {
            motor.Flip();
            facing = (int)motor.FacingDirection;
        }
        else
        {
            FaceDirection(-facing);
        }
    }

    private bool HasReachedPatrolBoundary()
    {
        float currentFacing = (motor != null) ? motor.FacingDirection : facing;

        Vector3 wallProbeOrigin = wallCheck != null ? wallCheck.position : transform.position;
        bool wallAhead = CheckRay(wallProbeOrigin, new Vector2(currentFacing, 0f), wallCheckDistance);
        if (wallAhead)
            return true;

        if (groundCheck == null)
            return false;

        if (!HasGroundBelow(groundCheck.position))
            return true;

        if (wallCheck == null)
            return false;

        Vector3 ledgeProbeOrigin = groundCheck.position;
        ledgeProbeOrigin.x = wallCheck.position.x;
        return !HasGroundBelow(ledgeProbeOrigin);
    }

    private bool HasGroundBelow(Vector3 origin)
    {
        return CheckRay(origin, Vector2.down, groundCheckDistance);
    }

    private bool CheckRay(Vector3 origin, Vector2 dir, float length)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, length, GetTerrainMask());
        Debug.DrawRay(origin, dir * length, hit.collider != null ? Color.red : Color.green);
        return hit.collider != null;
    }

    private LayerMask GetTerrainMask()
    {
        if (config != null && config.terrainLayers.value != 0)
        {
            return config.terrainLayers;
        }
        return terrainMask;
    }

    private void PlayClip(AnimationClip clip)
    {
        if (animancer != null && clip != null)
            animancer.Play(clip);
    }

    private void ApplyDeathVisualOffset()
    {
        if (deathVisualOffsetApplied || deathVisualOffset == Vector2.zero)
        {
            return;
        }

        transform.position += (Vector3)deathVisualOffset;
        deathVisualOffsetApplied = true;
    }
}
