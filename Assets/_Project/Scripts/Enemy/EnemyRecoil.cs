using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyRecoil : MonoBehaviour
{
    public event Action OnRecoilEnded;

    [SerializeField] private EnemyRecoilState state = EnemyRecoilState.Ready;

    private EnemyConfig config;
    private EnemyStateBlackboard blackboard;
    private Rigidbody2D body;
    private Coroutine recoilCoroutine;

    private EnemyMotor motor;

    public bool IsRecoiling => state == EnemyRecoilState.Recoiling || state == EnemyRecoilState.Frozen;
    public EnemyRecoilState State => state;

    public void Initialize(EnemyConfig enemyConfig, EnemyStateBlackboard stateBlackboard, Rigidbody2D rigidbody)
    {
        config = enemyConfig;
        blackboard = stateBlackboard;
        body = rigidbody;
        motor = GetComponent<EnemyMotor>();
    }

    public void RecoilFromHit(HeroAttackHit hit)
    {
        if (body == null || config == null || blackboard == null || blackboard.dead)
        {
            return;
        }

        if (config.preventUpwardRecoil && IsUpwardRecoil(hit.ForceDirection))
        {
            return;
        }

        if (recoilCoroutine != null)
        {
            StopCoroutine(recoilCoroutine);
        }

        ApplyHitVelocity(hit);
        recoilCoroutine = StartCoroutine(RecoilRoutine());
    }

    public void CancelRecoil()
    {
        bool wasRecoiling = IsRecoiling;

        if (recoilCoroutine != null)
        {
            StopCoroutine(recoilCoroutine);
            recoilCoroutine = null;
        }

        if (blackboard != null)
        {
            blackboard.hurt = false;
            blackboard.recoiling = false;
        }

        state = EnemyRecoilState.Ready;

        if (motor != null)
        {
            motor.ClearExternalVelocity();
        }

        if (wasRecoiling)
        {
            OnRecoilEnded?.Invoke();
        }
    }

    private void ApplyHitVelocity(HeroAttackHit hit)
    {
        if (motor != null)
        {
            if (config.freezeOnHit)
            {
                state = EnemyRecoilState.Frozen;
                motor.ApplyExternalVelocity(Vector2.zero);
                return;
            }

            state = EnemyRecoilState.Recoiling;
            Vector2 knockback = hit.ForceDirection * config.knockbackForce;
            if (config.stopHorizontalVelocityOnUpwardRecoil && IsUpwardRecoil(hit.ForceDirection))
            {
                knockback.x = 0f;
            }

            if (Mathf.Abs(hit.ForceDirection.y) < 0.5f)
            {
                knockback.y += config.knockbackLift;
            }

            motor.ApplyExternalVelocity(knockback);
            return;
        }

        if (config.freezeOnHit)
        {
            state = EnemyRecoilState.Frozen;
            body.linearVelocity = Vector2.zero;
            return;
        }

        state = EnemyRecoilState.Recoiling;
        Vector2 fallbackKnockback = hit.ForceDirection * config.knockbackForce;
        if (config.stopHorizontalVelocityOnUpwardRecoil && IsUpwardRecoil(hit.ForceDirection))
        {
            fallbackKnockback.x = 0f;
        }

        if (Mathf.Abs(hit.ForceDirection.y) < 0.5f)
        {
            fallbackKnockback.y += config.knockbackLift;
        }

        body.linearVelocity = fallbackKnockback;
    }

    private IEnumerator RecoilRoutine()
    {
        blackboard.hurt = true;
        blackboard.recoiling = true;
        yield return new WaitForSeconds(config.stunDuration);
        blackboard.hurt = false;
        blackboard.recoiling = false;
        recoilCoroutine = null;
        state = EnemyRecoilState.Ready;
        if (motor != null)
        {
            motor.ClearExternalVelocity();
        }
        OnRecoilEnded?.Invoke();
    }

    private static bool IsUpwardRecoil(Vector2 forceDirection)
    {
        return forceDirection.y > 0.5f;
    }
}
