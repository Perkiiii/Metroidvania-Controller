using System.Collections.Generic;
using UnityEngine;

public sealed class HeroAttackAction
{
    private const int MaxAttackHits = 16;

    private readonly Collider2D[] hitBuffer = new Collider2D[MaxAttackHits];
    private readonly HashSet<Collider2D> hitColliders = new HashSet<Collider2D>();
    private readonly HeroConfig config;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly GameObject owner;
    private readonly Transform ownerTransform;

    private float attackTimer;
    private float cooldownTimer;
    private float recoveryTimer;
    private HeroAttackDirection currentDirection;

    public HeroAttackAction(
        HeroConfig heroConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        GameObject ownerObject,
        Transform ownerRoot)
    {
        config = heroConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        owner = ownerObject;
        ownerTransform = ownerRoot;
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

        if (input.AttackPressedThisFrame && CanStartAttack())
        {
            StartAttack();
        }
    }

    public void FixedTick(float fixedDeltaTime)
    {
        if (!blackboard.attacking)
        {
            return;
        }

        attackTimer += fixedDeltaTime;

        if (attackTimer >= config.attackHitboxStartTime && attackTimer <= config.attackHitboxEndTime)
        {
            EvaluateHitbox();
        }

        if (attackTimer >= config.attackDuration)
        {
            StopAttack();
        }
    }

    public void DrawGizmosSelected()
    {
        if (config == null || blackboard == null || ownerTransform == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(GetHitboxCenter(), GetHitboxSize());
    }

    private bool CanStartAttack()
    {
        return cooldownTimer <= 0f
            && !blackboard.controlLocked
            && !blackboard.inputBlocked
            && !blackboard.attacking
            && !blackboard.dashing;
    }

    private void StartAttack()
    {
        currentDirection = DetermineAttackDirection();
        blackboard.attacking = true;
        blackboard.upAttacking = currentDirection == HeroAttackDirection.Up;
        blackboard.downAttacking = currentDirection == HeroAttackDirection.Down;
        blackboard.attackDirection = currentDirection;

        attackTimer = 0f;
        cooldownTimer = config.attackCooldown;
        recoveryTimer = config.attackRecovery;
        blackboard.attackRecovering = true;
        hitColliders.Clear();
    }

    private void StopAttack()
    {
        blackboard.attacking = false;
        blackboard.upAttacking = false;
        blackboard.downAttacking = false;
        hitColliders.Clear();
    }

    private HeroAttackDirection DetermineAttackDirection()
    {
        if (input.MoveVector.y >= config.attackDirectionThreshold)
        {
            return HeroAttackDirection.Up;
        }

        if (!blackboard.grounded && input.MoveVector.y <= -config.attackDirectionThreshold)
        {
            return HeroAttackDirection.Down;
        }

        return HeroAttackDirection.Side;
    }

    private void EvaluateHitbox()
    {
        Vector2 center = GetHitboxCenter();
        Vector2 size = GetHitboxSize();
        int hitCount = Physics2D.OverlapBoxNonAlloc(center, size, 0f, hitBuffer, config.attackHitLayers);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = hitBuffer[i];
            if (hitCollider == null || hitColliders.Contains(hitCollider))
            {
                continue;
            }

            IHeroAttackReceiver receiver = FindAttackReceiver(hitCollider);
            if (receiver == null)
            {
                continue;
            }

            hitColliders.Add(hitCollider);
            receiver.ReceiveHeroAttack(new HeroAttackHit(
                owner,
                currentDirection,
                config.attackDamage,
                hitCollider.ClosestPoint(center),
                GetForceDirection()));
        }
    }

    private Vector2 GetHitboxCenter()
    {
        Vector2 offset = currentDirection switch
        {
            HeroAttackDirection.Up => config.attackUpOffset,
            HeroAttackDirection.Down => config.attackDownOffset,
            _ => new Vector2(config.attackSideOffset.x * blackboard.FacingDirection, config.attackSideOffset.y)
        };

        return (Vector2)ownerTransform.position + offset;
    }

    private Vector2 GetHitboxSize()
    {
        return currentDirection switch
        {
            HeroAttackDirection.Up => config.attackUpSize,
            HeroAttackDirection.Down => config.attackDownSize,
            _ => config.attackSideSize
        };
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

    private static IHeroAttackReceiver FindAttackReceiver(Collider2D hitCollider)
    {
        MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IHeroAttackReceiver receiver)
            {
                return receiver;
            }
        }

        return null;
    }
}
