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

    public bool IsRecoiling => state == EnemyRecoilState.Recoiling || state == EnemyRecoilState.Frozen;
    public EnemyRecoilState State => state;

    public void Initialize(EnemyConfig enemyConfig, EnemyStateBlackboard stateBlackboard, Rigidbody2D rigidbody)
    {
        config = enemyConfig;
        blackboard = stateBlackboard;
        body = rigidbody;
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

        if (wasRecoiling)
        {
            OnRecoilEnded?.Invoke();
        }
    }

    private void ApplyHitVelocity(HeroAttackHit hit)
    {
        if (config.freezeOnHit)
        {
            state = EnemyRecoilState.Frozen;
            body.linearVelocity = Vector2.zero;
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

        body.linearVelocity = knockback;
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
        OnRecoilEnded?.Invoke();
    }

    private static bool IsUpwardRecoil(Vector2 forceDirection)
    {
        return forceDirection.y > 0.5f;
    }
}
