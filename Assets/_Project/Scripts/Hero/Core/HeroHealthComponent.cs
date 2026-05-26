using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroHealthComponent : MonoBehaviour
{
    public event Action<int, Vector2> OnDamaged;
    public event Action<int, int> OnHealthChanged;
    public event Action<DamageResult> OnHazardDamaged;
    public event Action OnDeath;

    private HeroConfig config;
    private int currentHealth;
    private readonly HashSet<object> iFrameSources = new HashSet<object>();
    private readonly Dictionary<object, Coroutine> iFrameCoroutines = new Dictionary<object, Coroutine>();

    public int CurrentHealth => currentHealth;
    public int MaxHealth => config != null ? config.maxHealth : 0;
    public bool IsInvincible => iFrameSources.Count > 0;

    public void Initialize(HeroConfig heroConfig)
    {
        config = heroConfig;
        currentHealth = config.maxHealth;
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
            OnDamaged?.Invoke(currentHealth, knockbackForce);
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
        if (currentHealth <= 0)
        {
            return;
        }

        int previousHealth = currentHealth;
        currentHealth = 0;
        NotifyHealthChanged(previousHealth);
        OnDeath?.Invoke();
    }

    public void RestoreFullHealth()
    {
        if (config == null)
        {
            return;
        }

        int previousHealth = currentHealth;
        currentHealth = config.maxHealth;
        NotifyHealthChanged(previousHealth);
    }

    private DamageResult ApplyDamage(int amount)
    {
        if (amount <= 0 || currentHealth <= 0)
        {
            return new DamageResult(currentHealth, currentHealth, 0, true);
        }

        int previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        NotifyHealthChanged(previousHealth);
        return new DamageResult(previousHealth, currentHealth, previousHealth - currentHealth, false);
    }

    private void NotifyHealthChanged(int previousHealth)
    {
        if (previousHealth == currentHealth)
        {
            return;
        }

        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
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
}
