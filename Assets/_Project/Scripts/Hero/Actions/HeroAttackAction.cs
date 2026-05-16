using System.Collections.Generic;
using UnityEngine;

public sealed class HeroAttackAction
{
    private const int DefaultMaxAttackHits = 16;

    private readonly Collider2D[] damageHitBuffer;
    private readonly Collider2D[] clashHitBuffer;
    private readonly HashSet<IHeroAttackReceiver> hitReceivers = new HashSet<IHeroAttackReceiver>();
    private readonly HashSet<IHeroAttackClashReceiver> clashReceivers = new HashSet<IHeroAttackClashReceiver>();
    private readonly HeroConfig config;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly HeroMotor motor;
    private readonly GameObject owner;
    private readonly Transform ownerTransform;
    private readonly HeroAttackModule[] attackModules;
    private readonly float failSafeTimeout;

    private float attackTimer;
    private float attackFallbackTimeout;
    private float cooldownTimer;
    private float recoveryTimer;
    private bool attackWindowActive;
    private bool downslashBounceConsumedThisAttack;
    private int attackVersion;
    private HeroAttackDirection currentDirection;
    private HeroAttackModule currentModule;

    public int AttackVersion => attackVersion;

    public HeroAttackAction(
        HeroConfig heroConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor,
        GameObject ownerObject,
        Transform ownerRoot,
        HeroAttackModule[] modules,
        float attackFailSafeTimeout)
    {
        config = heroConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        motor = heroMotor;
        owner = ownerObject;
        ownerTransform = ownerRoot;
        attackModules = modules ?? new HeroAttackModule[0];
        failSafeTimeout = Mathf.Max(0.1f, attackFailSafeTimeout);

        int maxHits = Mathf.Max(1, config != null ? config.maxHitsPerSwing : DefaultMaxAttackHits);
        damageHitBuffer = new Collider2D[maxHits];
        clashHitBuffer = new Collider2D[maxHits];
        DeactivateAllModules();
    }

    public void Tick(float deltaTime)
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= deltaTime;
        }

        if (recoveryTimer > 0f)
        {
            recoveryTimer -= deltaTime;
        }

        blackboard.attackRecovering = recoveryTimer > 0f;

        if (input.HasBufferedAttack && CanStartAttack())
        {
            input.ConsumeAttackBuffer();
            StartAttack();
        }
    }

    public void FixedTick(float fixedDeltaTime)
    {
        if (!blackboard.attacking || currentModule == null)
        {
            return;
        }

        attackTimer += fixedDeltaTime;

        if (attackWindowActive)
        {
            currentModule.SetHitWindowActive(true);
            EvaluateDamageCollider();
            EvaluateClashCollider();
        }

        if (attackTimer >= attackFallbackTimeout)
        {
            ForceStopAttackFromFailSafe();
        }
    }

    public void CancelAttack()
    {
        EndAttack(true);
    }

    public void BeginAttackWindow()
    {
        if (!blackboard.attacking || currentModule == null)
        {
            return;
        }

        attackWindowActive = true;
        currentModule.SetHitWindowActive(true);
        EvaluateDamageCollider();
        EvaluateClashCollider();
    }

    public void EndAttackWindow()
    {
        attackWindowActive = false;

        if (currentModule != null)
        {
            currentModule.SetHitWindowActive(false);
        }
    }

    public void CompleteAttackFromAnimation()
    {
        if (!blackboard.attacking)
        {
            return;
        }

        StopAttack();
    }

    public void SetFallbackTimeout(float timeout)
    {
        if (blackboard.attacking && timeout > 0f)
        {
            attackFallbackTimeout = Mathf.Max(attackTimer, timeout);
        }
    }

    public void DrawGizmosSelected()
    {
        if (attackModules == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        for (int i = 0; i < attackModules.Length; i++)
        {
            DrawPolygonGizmo(attackModules[i] != null ? attackModules[i].damageCollider : null);
        }

        Gizmos.color = Color.yellow;
        for (int i = 0; i < attackModules.Length; i++)
        {
            DrawPolygonGizmo(attackModules[i] != null ? attackModules[i].clashCollider : null);
        }
    }

    private bool CanStartAttack()
    {
        return cooldownTimer <= 0f
            && !blackboard.controlLocked
            && !blackboard.inputBlocked
            && !blackboard.attacking
            && !blackboard.dashing
            && !blackboard.wallJumping;
    }

    private void StartAttack()
    {
        currentDirection = DetermineAttackDirection();
        currentModule = FindModule(currentDirection);

        if (currentModule == null || !currentModule.HasDamageCollider)
        {
            Debug.LogWarning($"Hero attack skipped because no {currentDirection} attack module with a damage collider is configured.", owner);
            currentModule = null;
            return;
        }

        blackboard.attacking = true;
        blackboard.upAttacking = currentDirection == HeroAttackDirection.Up;
        blackboard.downAttacking = currentDirection == HeroAttackDirection.Down;
        blackboard.attackDirection = currentDirection;
        attackVersion++;

        attackTimer = 0f;
        attackFallbackTimeout = failSafeTimeout;
        attackWindowActive = false;
        downslashBounceConsumedThisAttack = false;
        cooldownTimer = config.attackCooldown;
        recoveryTimer = config.attackRecovery;
        blackboard.attackRecovering = true;
        hitReceivers.Clear();
        clashReceivers.Clear();
        DeactivateAllModules();
        currentModule.SetAttackWindowCallbacks(BeginAttackWindow, EndAttackWindow);
        currentModule.Activate(blackboard.FacingDirection);
    }

    private void StopAttack()
    {
        EndAttack(false);
    }

    private void ForceStopAttackFromFailSafe()
    {
        Debug.LogWarning("Attack fail-safe triggered. Attack was force-ended because animation completion did not arrive in time.", owner);
        EndAttack(false);
    }

    private void EndAttack(bool clearRecovery)
    {
        DeactivateAllModules();
        currentModule = null;
        attackTimer = 0f;
        attackFallbackTimeout = 0f;
        attackWindowActive = false;
        downslashBounceConsumedThisAttack = false;
        blackboard.attacking = false;
        blackboard.upAttacking = false;
        blackboard.downAttacking = false;
        hitReceivers.Clear();
        clashReceivers.Clear();

        if (clearRecovery)
        {
            recoveryTimer = 0f;
            blackboard.attackRecovering = false;
        }
    }

    private HeroAttackDirection DetermineAttackDirection()
    {
        if (input.MoveVector.y >= config.attackDirectionThreshold)
        {
            return HeroAttackDirection.Up;
        }

        if (input.MoveVector.y <= -config.attackDirectionThreshold)
        {
            return HeroAttackDirection.Down;
        }

        return HeroAttackDirection.Side;
    }

    private void EvaluateDamageCollider()
    {
        ContactFilter2D contactFilter = currentModule.CreateDamageFilter(config.attackHitLayers);
        int hitCount = currentModule.DamageCollider.Overlap(contactFilter, damageHitBuffer);
        Vector2 referencePoint = currentModule.GetDamageReferencePoint();

        for (int i = 0; i < hitCount && hitReceivers.Count < damageHitBuffer.Length; i++)
        {
            Collider2D hitCollider = damageHitBuffer[i];
            if (hitCollider == null || IsSelfCollider(hitCollider))
            {
                continue;
            }

            IHeroAttackReceiver receiver = FindComponentInParents<IHeroAttackReceiver>(hitCollider);
            if (receiver == null || !hitReceivers.Add(receiver))
            {
                continue;
            }

            HeroAttackHit hit = new HeroAttackHit(
                owner,
                currentDirection,
                config.attackDamage,
                hitCollider.ClosestPoint(referencePoint),
                GetForceDirection());

            receiver.ReceiveHeroAttack(hit);
            IHeroDownslashResponder downslashResponder = FindDownslashResponder(hitCollider, receiver);
            NotifyDownslashResponder(downslashResponder, hit);
            TryApplyDownslashBounce(downslashResponder);
        }
    }

    private void EvaluateClashCollider()
    {
        if (!currentModule.HasClashCollider)
        {
            return;
        }

        ContactFilter2D contactFilter = currentModule.CreateClashFilter(config.attackHitLayers);
        int hitCount = currentModule.ClashCollider.Overlap(contactFilter, clashHitBuffer);
        Vector2 referencePoint = currentModule.ClashCollider.bounds.center;

        for (int i = 0; i < hitCount && clashReceivers.Count < clashHitBuffer.Length; i++)
        {
            Collider2D hitCollider = clashHitBuffer[i];
            if (hitCollider == null || IsSelfCollider(hitCollider))
            {
                continue;
            }

            IHeroAttackClashReceiver receiver = FindComponentInParents<IHeroAttackClashReceiver>(hitCollider);
            if (receiver == null || !clashReceivers.Add(receiver))
            {
                continue;
            }

            receiver.ReceiveHeroAttackClash(new HeroAttackHit(
                owner,
                currentDirection,
                0,
                hitCollider.ClosestPoint(referencePoint),
                GetForceDirection()));
        }
    }

    private IHeroDownslashResponder FindDownslashResponder(Collider2D hitCollider, IHeroAttackReceiver attackReceiver)
    {
        if (currentDirection != HeroAttackDirection.Down)
        {
            return null;
        }

        if (attackReceiver is IHeroDownslashResponder directResponder)
        {
            return directResponder;
        }

        return FindComponentInParents<IHeroDownslashResponder>(hitCollider);
    }

    private static void NotifyDownslashResponder(IHeroDownslashResponder responder, HeroAttackHit hit)
    {
        responder?.ReceiveHeroDownslash(hit);
    }

    private void TryApplyDownslashBounce(IHeroDownslashResponder responder)
    {
        if (responder == null
            || downslashBounceConsumedThisAttack
            || currentDirection != HeroAttackDirection.Down
            || blackboard.grounded)
        {
            return;
        }

        downslashBounceConsumedThisAttack = true;
        motor?.ApplyDownslashBounce();
    }

    private Vector2 GetForceDirection()
    {
        return currentDirection switch
        {
            HeroAttackDirection.Up => Vector2.up,
            HeroAttackDirection.Down => Vector2.down,
            _ => new Vector2(blackboard.FacingDirection, 0f)
        };
    }

    private HeroAttackModule FindModule(HeroAttackDirection direction)
    {
        for (int i = 0; i < attackModules.Length; i++)
        {
            HeroAttackModule module = attackModules[i];
            if (module != null && module.direction == direction)
            {
                return module;
            }
        }

        return null;
    }

    private bool IsSelfCollider(Collider2D hitCollider)
    {
        Transform hitTransform = hitCollider.transform;
        return hitTransform == ownerTransform || hitTransform.IsChildOf(ownerTransform);
    }

    private void DeactivateAllModules()
    {
        for (int i = 0; i < attackModules.Length; i++)
        {
            if (attackModules[i] != null)
            {
                attackModules[i].Deactivate();
            }
        }
    }

    private static T FindComponentInParents<T>(Collider2D hitCollider) where T : class
    {
        MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T receiver)
            {
                return receiver;
            }
        }

        return null;
    }

    private static void DrawPolygonGizmo(PolygonCollider2D polygon)
    {
        if (polygon == null)
        {
            return;
        }

        Transform polygonTransform = polygon.transform;
        for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
        {
            Vector2[] path = polygon.GetPath(pathIndex);
            for (int pointIndex = 0; pointIndex < path.Length; pointIndex++)
            {
                Vector3 from = polygonTransform.TransformPoint(path[pointIndex]);
                Vector3 to = polygonTransform.TransformPoint(path[(pointIndex + 1) % path.Length]);
                Gizmos.DrawLine(from, to);
            }
        }
    }
}
