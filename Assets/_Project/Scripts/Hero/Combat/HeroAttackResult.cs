using System;

public enum HeroAttackOutcome
{
    Ignored,
    Blocked,
    Invulnerable,
    Damaged,
    Killed
}

public readonly struct HeroAttackResult
{
    public HeroAttackOutcome Outcome { get; }
    public int DamageApplied { get; }
    public bool ResourceEligible { get; }

    public bool WasAccepted => Outcome == HeroAttackOutcome.Damaged || Outcome == HeroAttackOutcome.Killed;

    private HeroAttackResult(HeroAttackOutcome outcome, int damageApplied, bool resourceEligible)
    {
        Outcome = outcome;
        DamageApplied = Math.Max(0, damageApplied);
        ResourceEligible = resourceEligible && (outcome == HeroAttackOutcome.Damaged || outcome == HeroAttackOutcome.Killed);
    }

    public static HeroAttackResult Ignored => new HeroAttackResult(HeroAttackOutcome.Ignored, 0, false);
    public static HeroAttackResult Blocked => new HeroAttackResult(HeroAttackOutcome.Blocked, 0, false);
    public static HeroAttackResult Invulnerable => new HeroAttackResult(HeroAttackOutcome.Invulnerable, 0, false);

    public static HeroAttackResult Damaged(int damageApplied)
    {
        return new HeroAttackResult(HeroAttackOutcome.Damaged, damageApplied, true);
    }

    public static HeroAttackResult Killed(int damageApplied)
    {
        return new HeroAttackResult(HeroAttackOutcome.Killed, damageApplied, true);
    }
}
