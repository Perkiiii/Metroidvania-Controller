using System.Collections;
using Animancer;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class MushroomEnemy : MonoBehaviour, IEnemyBehaviour
{
    private static readonly WaitForFixedUpdate FixedUpdateWait = new WaitForFixedUpdate();

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
    [SerializeField] private AnimationClip hitClip;
    [SerializeField] private AnimationClip dieClip;
    [SerializeField] private Vector2 deathVisualOffset = Vector2.zero;

    private Rigidbody2D body;
    private EnemyStateBlackboard blackboard;
    private AnimancerComponent animancer;
    private EnemyHealthComponent health;
    private int facing = 1;
    private bool deathVisualOffsetApplied;

    public void Initialize(EnemyStateBlackboard stateBlackboard, Rigidbody2D rb)
    {
        blackboard = stateBlackboard;
        body = rb;
        SubscribeToHealth();
    }

    private void Awake()
    {
        // Cache body here as well because Initialize may not have fired before EnemyController.Awake.
        body = GetComponent<Rigidbody2D>();
        animancer = GetComponent<AnimancerComponent>();
    }

    private void OnDestroy()
    {
        UnsubscribeFromHealth();
    }

    private void Start()
    {
        if (body == null)
        {
            Debug.LogError($"MushroomEnemy on '{name}' has no Rigidbody2D. Movement disabled.", this);
            return;
        }

        StartCoroutine(WalkLoop());
    }

    private IEnumerator WalkLoop()
    {
        while (true)
        {
            if (blackboard != null && blackboard.dead)
                yield break;

            if (IsMovementSuppressed())
            {
                yield return null;
                continue;
            }

            PlayClip(walkClip);

            bool hitBoundary = false;
            while (!hitBoundary)
            {
                if (blackboard != null && blackboard.dead)
                {
                    yield break;
                }

                if (IsMovementSuppressed())
                {
                    break;
                }

                hitBoundary = HasReachedPatrolBoundary();
                if (!hitBoundary)
                {
                    body.linearVelocity = new Vector2(facing * speed, body.linearVelocity.y);
                }

                yield return FixedUpdateWait;
            }

            if (IsMovementSuppressed())
            {
                continue;
            }

            yield return StartCoroutine(Turn());
        }
    }

    private IEnumerator Turn()
    {
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        PlayClip(idleClip);
        yield return new WaitForSeconds(0.15f);

        facing = -facing;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * facing;
        transform.localScale = s;
    }

    private void PlayClip(AnimationClip clip)
    {
        if (animancer != null && clip != null)
            animancer.Play(clip);
    }

    private void PlayHit()
    {
        PlayClip(hitClip);
    }

    private void PlayDeath()
    {
        ApplyDeathVisualOffset();
        PlayClip(dieClip);
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

    private void SubscribeToHealth()
    {
        EnemyHealthComponent nextHealth = GetComponent<EnemyHealthComponent>();
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

        health.OnDamaged += PlayHit;
        health.OnDeath += PlayDeath;
    }

    private void UnsubscribeFromHealth()
    {
        if (health == null)
        {
            return;
        }

        health.OnDamaged -= PlayHit;
        health.OnDeath -= PlayDeath;
    }

    private bool HasReachedPatrolBoundary()
    {
        Vector3 wallProbeOrigin = wallCheck != null ? wallCheck.position : transform.position;
        bool wallAhead = CheckRay(wallProbeOrigin, new Vector2(facing, 0f), wallCheckDistance);
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
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, length, terrainMask);
        Debug.DrawRay(origin, dir * length, hit.collider != null ? Color.red : Color.green);
        return hit.collider != null;
    }

    private bool IsMovementSuppressed()
    {
        return blackboard != null && (blackboard.hurt || blackboard.recoiling);
    }
}
