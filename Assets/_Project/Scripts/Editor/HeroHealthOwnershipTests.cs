using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class HeroHealthOwnershipTests
{
    private GameObject heroObject;
    private HeroConfig config;
    private PlayerHealthState state;
    private HeroHealthComponent health;

    [SetUp]
    public void SetUp()
    {
        config = ScriptableObject.CreateInstance<HeroConfig>();
        state = ScriptableObject.CreateInstance<PlayerHealthState>();
        state.ApplySaveData(CreateHealthData(current: 5, maximum: 5, bonus: 0));

        heroObject = new GameObject("HeroHealthComponent Test");
        health = heroObject.AddComponent<HeroHealthComponent>();
        health.Initialize(config, state);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(heroObject);
        Object.DestroyImmediate(state);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void DamageMutatesStateAndFiresContextEvent()
    {
        int eventCount = 0;
        int remainingHealth = -1;
        health.OnDamaged += (current, _) =>
        {
            eventCount++;
            remainingHealth = current;
        };

        health.TakeDamage(2, null);

        Assert.That(state.CurrentHealth, Is.EqualTo(3));
        Assert.That(health.CurrentHealth, Is.EqualTo(3));
        Assert.That(remainingHealth, Is.EqualTo(3));
        Assert.That(eventCount, Is.EqualTo(1));
    }

    [Test]
    public void DamageConsumesBonusBeforeNormalHealthThroughFacade()
    {
        state.ApplySaveData(CreateHealthData(current: 5, maximum: 5, bonus: 2));

        health.TakeDamage(1, null);

        Assert.That(state.CurrentHealth, Is.EqualTo(5));
        Assert.That(state.BonusHealth, Is.EqualTo(1));
        Assert.That(health.BonusHealth, Is.EqualTo(1));
    }

    [Test]
    public void HealMutatesStateWithNeutralHealReason()
    {
        state.ApplySaveData(CreateHealthData(current: 2, maximum: 5, bonus: 0));
        PlayerHealthChangeReason? reason = null;
        state.Changed += info => reason = info.Reason;

        int healed = health.Heal(2);

        Assert.That(healed, Is.EqualTo(2));
        Assert.That(state.CurrentHealth, Is.EqualTo(4));
        Assert.That(reason, Is.EqualTo(PlayerHealthChangeReason.Heal));
    }

    [Test]
    public void LethalDamageFiresDeathOnlyOnce()
    {
        int deathCount = 0;
        health.OnDeath += () => deathCount++;

        health.TakeDamage(5, null);
        health.TakeDamage(1, null);

        Assert.That(state.CurrentHealth, Is.Zero);
        Assert.That(deathCount, Is.EqualTo(1));
    }

    [Test]
    public void InvincibilitySourceRejectsNormalDamage()
    {
        HashSet<object> sources = GetPrivateField<HashSet<object>>(health, "iFrameSources");
        sources.Add(new object());

        health.TakeDamage(1, null);

        Assert.That(state.CurrentHealth, Is.EqualTo(5));
    }

    [Test]
    public void HazardDamageMutatesStateAndFiresOnlyHazardContext()
    {
        int hazardCount = 0;
        int normalCount = 0;
        health.OnHazardDamaged += _ => hazardCount++;
        health.OnDamaged += (_, __) => normalCount++;

        DamageResult result = health.TakeHazardDamage(2, this);

        Assert.That(result.WasIgnored, Is.False);
        Assert.That(result.DamageApplied, Is.EqualTo(2));
        Assert.That(state.CurrentHealth, Is.EqualTo(3));
        Assert.That(hazardCount, Is.EqualTo(1));
        Assert.That(normalCount, Is.Zero);
    }

    [Test]
    public void ForcedHazardDeathClearsAllHealthWithOneDistinctNotification()
    {
        state.ApplySaveData(CreateHealthData(current: 4, maximum: 5, bonus: 3));
        int stateChangeCount = 0;
        int deathCount = 0;
        PlayerHealthChangeReason? reason = null;
        state.Changed += info =>
        {
            stateChangeCount++;
            reason = info.Reason;
        };
        health.OnDeath += () => deathCount++;

        health.TriggerHazardDeath();
        health.TriggerHazardDeath();

        Assert.That(state.CurrentHealth, Is.Zero);
        Assert.That(state.BonusHealth, Is.Zero);
        Assert.That(stateChangeCount, Is.EqualTo(1));
        Assert.That(reason, Is.EqualTo(PlayerHealthChangeReason.ForcedDepletion));
        Assert.That(deathCount, Is.EqualTo(1));
    }

    [Test]
    public void DeathRestoreRefillsNormalHealthAndExplicitlyClearsBonus()
    {
        state.ApplySaveData(CreateHealthData(current: 0, maximum: 7, bonus: 3));
        int stateChangeCount = 0;
        state.Changed += _ => stateChangeCount++;

        health.RestoreAfterDeath();

        Assert.That(state.CurrentHealth, Is.EqualTo(7));
        Assert.That(state.MaximumHealth, Is.EqualTo(7));
        Assert.That(state.BonusHealth, Is.Zero);
        Assert.That(stateChangeCount, Is.EqualTo(1));
    }

    [Test]
    public void InitializePreservesAlreadyLoadedState()
    {
        state.ApplySaveData(CreateHealthData(current: 2, maximum: 8, bonus: 1));

        health.Initialize(config, state);

        Assert.That(state.CurrentHealth, Is.EqualTo(2));
        Assert.That(state.MaximumHealth, Is.EqualTo(8));
        Assert.That(state.BonusHealth, Is.EqualTo(1));
    }

    [Test]
    public void ApplyingSaveDataDoesNotFireGameplayContextEvents()
    {
        int gameplayEventCount = 0;
        health.OnDamaged += (_, __) => gameplayEventCount++;
        health.OnHazardDamaged += _ => gameplayEventCount++;
        health.OnDeath += () => gameplayEventCount++;

        state.ApplySaveData(CreateHealthData(current: 2, maximum: 6, bonus: 1));

        Assert.That(gameplayEventCount, Is.Zero);
        Assert.That(health.CurrentHealth, Is.EqualTo(2));
        Assert.That(health.MaxHealth, Is.EqualTo(6));
        Assert.That(health.BonusHealth, Is.EqualTo(1));
    }

    [Test]
    public void RecreatingSceneFacadeDoesNotResetPersistentState()
    {
        health.TakeDamage(2, null);
        GameObject replacementObject = new GameObject("Replacement HeroHealthComponent Test");
        try
        {
            HeroHealthComponent replacement = replacementObject.AddComponent<HeroHealthComponent>();
            replacement.Initialize(config, state);

            Assert.That(replacement.CurrentHealth, Is.EqualTo(3));
            Assert.That(state.CurrentHealth, Is.EqualTo(3));
        }
        finally
        {
            Object.DestroyImmediate(replacementObject);
        }
    }

    [Test]
    public void SaveLoadThenInitializePreservesSavedHealth()
    {
        SaveData save = new SaveData();
        state.ApplySaveData(CreateHealthData(current: 3, maximum: 9, bonus: 2));
        state.GatherSaveData(save);

        PlayerHealthState loadedState = ScriptableObject.CreateInstance<PlayerHealthState>();
        GameObject loadedHero = new GameObject("Loaded HeroHealthComponent Test");
        try
        {
            loadedState.ApplySaveData(save);
            HeroHealthComponent loadedHealth = loadedHero.AddComponent<HeroHealthComponent>();
            loadedHealth.Initialize(config, loadedState);

            Assert.That(loadedHealth.CurrentHealth, Is.EqualTo(3));
            Assert.That(loadedHealth.MaxHealth, Is.EqualTo(9));
            Assert.That(loadedHealth.BonusHealth, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(loadedHero);
            Object.DestroyImmediate(loadedState);
        }
    }

    [Test]
    public void FacadeDoesNotContainMirroredHealthStorage()
    {
        FieldInfo field = typeof(HeroHealthComponent).GetField(
            "currentHealth",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.That(field, Is.Null);
    }

    private static SaveData CreateHealthData(int current, int maximum, int bonus)
    {
        return new SaveData
        {
            health = new HealthSaveData
            {
                initialized = true,
                currentHealth = current,
                maximumHealth = maximum,
                bonusHealth = bonus
            }
        };
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        return (T)target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(target);
    }
}
