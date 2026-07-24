using System;
using System.Collections.Generic;

public readonly struct BossHudShowRequest
{
    public BossHudShowRequest(
        object source,
        BossEncounterDefinition definition,
        IReadOnlyList<EnemyHealthComponent> healthRoster)
    {
        Source = source;
        Definition = definition;
        HealthRoster = healthRoster;
    }

    public object Source { get; }
    public BossEncounterDefinition Definition { get; }
    public IReadOnlyList<EnemyHealthComponent> HealthRoster { get; }
}

public static class BossHudEventService
{
    public static event Action<BossHudShowRequest> ShowRequested;
    public static event Action<object> HideRequested;

    public static void RequestShow(
        object source,
        BossEncounterDefinition definition,
        IReadOnlyList<EnemyHealthComponent> healthRoster)
    {
        ShowRequested?.Invoke(new BossHudShowRequest(source, definition, healthRoster));
    }

    public static void RequestHide(object source)
    {
        HideRequested?.Invoke(source);
    }
}
