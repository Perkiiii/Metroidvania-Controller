public readonly struct DamageResult
{
    public DamageResult(int previousHealth, int currentHealth, int damageApplied, bool wasIgnored)
    {
        PreviousHealth = previousHealth;
        CurrentHealth = currentHealth;
        DamageApplied = damageApplied;
        WasIgnored = wasIgnored;
    }

    public int PreviousHealth { get; }
    public int CurrentHealth { get; }
    public int DamageApplied { get; }
    public bool WasIgnored { get; }
    public bool IsFatal => !WasIgnored && CurrentHealth <= 0;
}
