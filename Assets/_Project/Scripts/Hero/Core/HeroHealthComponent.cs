using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroHealthComponent : MonoBehaviour
{
    public event Action<int, Vector2> OnDamaged;
    public event Action<DamageResult> OnHazardDamaged;
    public event Action OnDeath;

    private HeroConfig config;
    private PlayerHealthState healthState;
    private readonly HashSet<object> iFrameSources = new HashSet<object>();
    private readonly Dictionary<object, Coroutine> iFrameCoroutines = new Dictionary<object, Coroutine>();

    public int CurrentHealth => healthState != null ? healthState.CurrentHealth : 0;
    public int MaxHealth => healthState != null ? healthState.MaximumHealth : 0;
    public int BonusHealth => healthState != null ? healthState.BonusHealth : 0;
    public bool IsInvincible => iFrameSources.Count > 0;

    public void Initialize(HeroConfig heroConfig, PlayerHealthState playerHealthState)
    {
        config = heroConfig;
        healthState = playerHealthState;
        ClearIFrames();

        if (healthState == null)
            Debug.LogError("[HeroHealthComponent] PlayerHealthState is not assigned. Health gameplay will remain inactive.", this);
    }

    public void TakeDamage(int amount, object iFrameSource, Vector2 knockbackForce = default)
    {
        if (IsInvincible || amount <= 0)
        {
            return;
        }

        DamageResult result = ApplyDamage(amount);
        if (result.WasIgnored)
        {
            return;
        }

        if (iFrameSource != null)
        {
            GrantIFrames(iFrameSource);
        }

        if (result.IsFatal)
        {
            OnDeath?.Invoke();
        }
        else
        {
            OnDamaged?.Invoke(CurrentHealth, knockbackForce);
        }
    }

    public DamageResult TakeHazardDamage(int amount, object source)
    {
        _ = source;
        DamageResult result = ApplyDamage(amount);
        if (result.IsFatal)
        {
            OnDeath?.Invoke();
        }
        else if (!result.WasIgnored)
        {
            OnHazardDamaged?.Invoke(result);
        }

        return result;
    }

    // Bypasses health and invincibility — called by HazardZone on pit/kill-zone contact.
    public void TriggerHazardDeath()
    {
        if (healthState == null || !healthState.ForceDeplete())
            return;

        OnDeath?.Invoke();
    }

    public void RestoreFullHealth()
    {
        healthState?.FullRestore();
    }

    public int Heal(int amount)
    {
        return healthState != null ? healthState.Heal(amount) : 0;
    }

    public void RestoreAfterDeath()
    {
        healthState?.FullRestore(preserveBonus: false);
    }

    private DamageResult ApplyDamage(int amount)
    {
        int currentHealth = CurrentHealth;
        if (healthState == null || amount <= 0 || healthState.IsDepleted)
            return new DamageResult(currentHealth, currentHealth, 0, true);

        int previousHealth = currentHealth;
        int damageApplied = healthState.ApplyDamage(amount);
        return new DamageResult(previousHealth, CurrentHealth, damageApplied, damageApplied <= 0);
    }

    // Grants invincibility for an explicit duration without requiring a damage hit.
    // Used by GameManager during hazard recovery to block enemy contact damage.
    public void GrantTemporaryInvincibility(object source, float duration)
    {
        if (source == null || duration <= 0f)
            return;
        GrantIFrames(source, duration);
    }

    public void GrantDefaultInvincibility(object source)
    {
        if (source == null)
            return;
        GrantIFrames(source);
    }

    private void GrantIFrames(object source)
    {
        GrantIFrames(source, config != null ? config.iFrameDuration : 0f);
    }

    private void GrantIFrames(object source, float duration)
    {
        if (duration <= 0f)
            return;
        if (iFrameCoroutines.TryGetValue(source, out Coroutine existing) && existing != null)
            StopCoroutine(existing);
        iFrameSources.Add(source);
        iFrameCoroutines[source] = StartCoroutine(IFrameRoutine(source, duration));
    }

    private IEnumerator IFrameRoutine(object source, float duration)
    {
        yield return new WaitForSeconds(duration);
        iFrameSources.Remove(source);
        iFrameCoroutines.Remove(source);
    }

    private void ClearIFrames()
    {
        foreach (Coroutine coroutine in iFrameCoroutines.Values)
        {
            if (coroutine != null)
                StopCoroutine(coroutine);
        }

        iFrameCoroutines.Clear();
        iFrameSources.Clear();
    }
}
