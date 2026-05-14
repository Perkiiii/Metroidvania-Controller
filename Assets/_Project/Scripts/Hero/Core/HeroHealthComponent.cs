using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroHealthComponent : MonoBehaviour
{
    public event Action<int, Vector2> OnDamaged;
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

        currentHealth = Mathf.Max(0, currentHealth - amount);

        if (iFrameSource != null)
        {
            GrantIFrames(iFrameSource);
        }

        if (currentHealth <= 0)
        {
            OnDeath?.Invoke();
        }
        else
        {
            OnDamaged?.Invoke(currentHealth, knockbackForce);
        }
    }

    // Bypasses health and invincibility — called by HazardZone on pit/kill-zone contact.
    public void TriggerHazardDeath()
    {
        currentHealth = 0;
        OnDeath?.Invoke();
    }

    public void RestoreFullHealth()
    {
        if (config == null)
        {
            return;
        }

        currentHealth = config.maxHealth;
    }

    private void GrantIFrames(object source)
    {
        if (iFrameCoroutines.TryGetValue(source, out Coroutine existing) && existing != null)
        {
            StopCoroutine(existing);
        }

        iFrameSources.Add(source);
        iFrameCoroutines[source] = StartCoroutine(IFrameRoutine(source));
    }

    private IEnumerator IFrameRoutine(object source)
    {
        yield return new WaitForSeconds(config.iFrameDuration);
        iFrameSources.Remove(source);
        iFrameCoroutines.Remove(source);
    }
}
