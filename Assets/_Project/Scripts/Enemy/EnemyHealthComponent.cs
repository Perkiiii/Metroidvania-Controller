using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyHealthComponent : MonoBehaviour, IHeroAttackReceiver, IHeroDownslashResponder
{
    public event Action OnDamaged;
    public event Action OnDeath;

    private EnemyConfig config;
    private EnemyStateBlackboard blackboard;
    private Rigidbody2D body;
    private EnemyRecoil recoil;
    private Collider2D[] colliders;
    private SpriteFlash flasher;
    private int currentHealth;

    public void Initialize(
        EnemyConfig enemyConfig,
        EnemyStateBlackboard stateBlackboard,
        Rigidbody2D rigidbody,
        EnemyRecoil enemyRecoil)
    {
        config = enemyConfig;
        blackboard = stateBlackboard;
        body = rigidbody;
        recoil = enemyRecoil;
        colliders = GetComponentsInChildren<Collider2D>(true);
        flasher = GetComponentInChildren<SpriteFlash>(true);
        currentHealth = config.maxHealth;
    }

    public void ReceiveHeroAttack(HeroAttackHit hit)
    {
        if (blackboard.dead)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - hit.Damage);

        flasher?.FlashHit();
        AudioManager.Instance?.PlaySFX(config.hurtSfx);
        GameManager.Instance?.HitStop(config.hitStopDuration);

        if (currentHealth <= 0)
        {
            StartDeath();
        }
        else
        {
            recoil?.RecoilFromHit(hit);
            OnDamaged?.Invoke();
        }
    }

    public void ReceiveHeroDownslash(HeroAttackHit hit)
    {
    }

    private void StartDeath()
    {
        blackboard.dead = true;
        recoil?.CancelRecoil();

        // Zero velocity and make static before disabling colliders to prevent physics glitches
        body.linearVelocity = Vector2.zero;
        body.bodyType = RigidbodyType2D.Static;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        AudioManager.Instance?.PlaySFX(config.deathSfx);
        OnDeath?.Invoke();
        StartCoroutine(DestroyRoutine());
    }

    private IEnumerator DestroyRoutine()
    {
        yield return new WaitForSeconds(config.deathDestroyDelay);
        Destroy(gameObject);
    }
}
