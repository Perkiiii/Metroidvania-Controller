using System;
using UnityEngine;

public enum PlayerHealthChangeReason
{
    Damage,
    Heal,
    FullRestore,
    BonusGranted,
    BonusCleared,
    MaximumChanged,
    ForcedDepletion,
    StateApplied,
    Reset
}

public readonly struct PlayerHealthChangeInfo
{
    public PlayerHealthChangeInfo(
        int currentHealth,
        int maximumHealth,
        int bonusHealth,
        PlayerHealthChangeReason reason)
    {
        CurrentHealth = currentHealth;
        MaximumHealth = maximumHealth;
        BonusHealth = bonusHealth;
        Reason = reason;
    }

    public int CurrentHealth { get; }
    public int MaximumHealth { get; }
    public int BonusHealth { get; }
    public PlayerHealthChangeReason Reason { get; }
}

[CreateAssetMenu(menuName = "Project/Hero/Player Health State", fileName = "PlayerHealthState")]
public sealed class PlayerHealthState : ScriptableObject, ISaveTarget
{
    [Header("Fresh Save Defaults")]
    [SerializeField, Min(1)] private int initialMaximumHealth = 5;

    [Header("Runtime State")]
    [SerializeField, Min(0)] private int currentHealth = 5;
    [SerializeField, Min(1)] private int maximumHealth = 5;
    [SerializeField, Min(0)] private int bonusHealth;

    public event Action<PlayerHealthChangeInfo> Changed;

    public int CurrentHealth => currentHealth;
    public int MaximumHealth => maximumHealth;
    public int BonusHealth => bonusHealth;
    public bool IsDepleted => currentHealth <= 0;

    public bool CanHeal(int amount)
    {
        return amount > 0 && currentHealth < maximumHealth;
    }

    private void OnEnable()
    {
        NormalizeRuntimeState();
    }

    private void OnValidate()
    {
        initialMaximumHealth = Mathf.Max(1, initialMaximumHealth);
        NormalizeRuntimeState();
    }

    public int ApplyDamage(int amount)
    {
        if (amount <= 0)
            return 0;

        long previousTotal = (long)currentHealth + bonusHealth;
        int remainingDamage = amount;

        int absorbedByBonus = Mathf.Min(bonusHealth, remainingDamage);
        bonusHealth -= absorbedByBonus;
        remainingDamage -= absorbedByBonus;

        if (remainingDamage > 0)
            currentHealth = Mathf.Max(0, currentHealth - remainingDamage);

        int damageApplied = (int)(previousTotal - ((long)currentHealth + bonusHealth));
        if (damageApplied > 0)
            NotifyChanged(PlayerHealthChangeReason.Damage);

        return damageApplied;
    }

    public int Heal(int amount)
    {
        if (amount <= 0 || currentHealth >= maximumHealth)
            return 0;

        int previousHealth = currentHealth;
        currentHealth = (int)Math.Min((long)maximumHealth, (long)currentHealth + amount);
        NotifyChanged(PlayerHealthChangeReason.Heal);
        return currentHealth - previousHealth;
    }

    public void FullRestore(bool preserveBonus = true)
    {
        int nextBonus = preserveBonus ? bonusHealth : 0;
        ApplyState(maximumHealth, maximumHealth, nextBonus, PlayerHealthChangeReason.FullRestore);
    }

    public bool ForceDeplete()
    {
        if (currentHealth <= 0 && bonusHealth <= 0)
            return false;

        ApplyState(0, maximumHealth, 0, PlayerHealthChangeReason.ForcedDepletion);
        return true;
    }

    public int GrantBonusHealth(int amount)
    {
        if (amount <= 0)
            return 0;

        int previousBonus = bonusHealth;
        bonusHealth = (int)Math.Min(int.MaxValue, (long)bonusHealth + amount);
        if (bonusHealth != previousBonus)
            NotifyChanged(PlayerHealthChangeReason.BonusGranted);

        return bonusHealth - previousBonus;
    }

    public void ClearBonusHealth()
    {
        if (bonusHealth == 0)
            return;

        bonusHealth = 0;
        NotifyChanged(PlayerHealthChangeReason.BonusCleared);
    }

    public void SetMaximumHealth(int value, bool restoreToFull = false)
    {
        int nextMaximum = Mathf.Max(1, value);
        int nextCurrent = restoreToFull
            ? nextMaximum
            : Mathf.Clamp(currentHealth, 0, nextMaximum);

        ApplyState(nextCurrent, nextMaximum, bonusHealth, PlayerHealthChangeReason.MaximumChanged);
    }

    public void ResetToDefaults()
    {
        int validInitialMaximum = Mathf.Max(1, initialMaximumHealth);
        ApplyState(validInitialMaximum, validInitialMaximum, 0, PlayerHealthChangeReason.Reset, forceNotify: true);
    }

    public void GatherSaveData(SaveData data)
    {
        if (data == null)
            return;

        data.health ??= new HealthSaveData();
        data.health.initialized = true;
        data.health.currentHealth = currentHealth;
        data.health.maximumHealth = maximumHealth;
        data.health.bonusHealth = bonusHealth;
    }

    public void ApplySaveData(SaveData data)
    {
        HealthSaveData saved = data?.health;
        if (saved == null || !saved.initialized)
        {
            int validInitialMaximum = Mathf.Max(1, initialMaximumHealth);
            ApplyState(validInitialMaximum, validInitialMaximum, 0, PlayerHealthChangeReason.StateApplied, forceNotify: true);
            return;
        }

        int loadedMaximum = Mathf.Max(1, saved.maximumHealth);
        int loadedCurrent = Mathf.Clamp(saved.currentHealth, 0, loadedMaximum);
        int loadedBonus = Mathf.Max(0, saved.bonusHealth);
        ApplyState(loadedCurrent, loadedMaximum, loadedBonus, PlayerHealthChangeReason.StateApplied, forceNotify: true);
    }

    /// <summary>
    /// Corrects a loaded save that captured the hero at zero health (e.g. save-on-quit or a
    /// checkpoint save racing a death sequence). No-op unless already depleted, so it is safe
    /// to call unconditionally on every load. Reuses StateApplied rather than Heal/FullRestore
    /// so HUD listeners treat this as a neutral refresh, not player healing. Preserves the
    /// saved MaximumHealth (progression) and only restores CurrentHealth/BonusHealth.
    /// </summary>
    public bool NormalizeDepletedContinue()
    {
        if (!IsDepleted)
            return false;

        ApplyState(maximumHealth, maximumHealth, 0, PlayerHealthChangeReason.StateApplied, forceNotify: true);
        return true;
    }

    private void NormalizeRuntimeState()
    {
        maximumHealth = Mathf.Max(1, maximumHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maximumHealth);
        bonusHealth = Mathf.Max(0, bonusHealth);
    }

    private void ApplyState(
        int nextCurrent,
        int nextMaximum,
        int nextBonus,
        PlayerHealthChangeReason reason,
        bool forceNotify = false)
    {
        nextMaximum = Mathf.Max(1, nextMaximum);
        nextCurrent = Mathf.Clamp(nextCurrent, 0, nextMaximum);
        nextBonus = Mathf.Max(0, nextBonus);

        bool changed = currentHealth != nextCurrent
            || maximumHealth != nextMaximum
            || bonusHealth != nextBonus;

        currentHealth = nextCurrent;
        maximumHealth = nextMaximum;
        bonusHealth = nextBonus;

        if (changed || forceNotify)
            NotifyChanged(reason);
    }

    private void NotifyChanged(PlayerHealthChangeReason reason)
    {
        Changed?.Invoke(new PlayerHealthChangeInfo(currentHealth, maximumHealth, bonusHealth, reason));
    }
}
