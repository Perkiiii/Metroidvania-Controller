using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class HeroBindActionTests
{
    private GameObject heroObject;
    private HeroStateBlackboard blackboard;
    private HeroInputReader input;
    private PlayerHealthState health;
    private PlayerResourceState resource;
    private PlayerResourceConfig config;
    private PlayerAbilityState abilityState;
    private HeroBindAction bind;

    [SetUp]
    public void SetUp()
    {
        heroObject = new GameObject("Hero Bind Action Test");
        blackboard = heroObject.AddComponent<HeroStateBlackboard>();
        input = heroObject.AddComponent<HeroInputReader>();
        blackboard.grounded = true;
        blackboard.actorState = HeroActorState.Grounded;

        health = ScriptableObject.CreateInstance<PlayerHealthState>();
        health.ApplySaveData(CreateHealthData(4, 5, 0));

        resource = ScriptableObject.CreateInstance<PlayerResourceState>();
        resource.SetMaximumParts(10);
        resource.Gain(5);

        config = ScriptableObject.CreateInstance<PlayerResourceConfig>();
        config.bindCostParts = 3;
        config.bindHealAmount = 1;
        config.bindDuration = 1f;

        abilityState = ScriptableObject.CreateInstance<PlayerAbilityState>();
        abilityState.bindUnlocked = true;

        bind = CreateBind();
        SetInput(pressed: true, held: true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(heroObject);
        Object.DestroyImmediate(health);
        Object.DestroyImmediate(resource);
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(abilityState);
    }

    [Test]
    public void CannotStartWhenAbilityLocked()
    {
        abilityState.bindUnlocked = false;

        bind.Tick(0f);

        Assert.That(bind.IsBinding, Is.False);
    }

    [Test]
    public void CannotStartAtFullHealth()
    {
        health.FullRestore();

        bind.Tick(0f);

        Assert.That(bind.IsBinding, Is.False);
    }

    [Test]
    public void CannotStartWithoutEnoughResource()
    {
        resource.TrySpend(5);

        bind.Tick(0f);

        Assert.That(bind.IsBinding, Is.False);
    }

    [Test]
    public void CannotStartWhileAirborneOrIncompatible()
    {
        blackboard.grounded = false;
        bind.Tick(0f);
        Assert.That(bind.IsBinding, Is.False);

        blackboard.grounded = true;
        blackboard.attacking = true;
        bind.Tick(0f);
        Assert.That(bind.IsBinding, Is.False);
    }

    [Test]
    public void StartsAndRemainsActiveWhileHeld()
    {
        bind.Tick(0f);
        SetInput(pressed: false, held: true);
        bind.Tick(0.5f);

        Assert.That(bind.IsBinding, Is.True);
        Assert.That(resource.CurrentParts, Is.EqualTo(5));
        Assert.That(health.CurrentHealth, Is.EqualTo(4));
        Assert.That(blackboard.binding, Is.True);
    }

    [Test]
    public void ReleasingInputCancelsWithoutMutation()
    {
        bind.Tick(0f);
        SetInput(pressed: false, held: false);
        bind.Tick(0.1f);

        AssertCancelledWithoutMutation();
    }

    [Test]
    public void DamageHazardDeathControlLockAndGroundLossCancel()
    {
        bind.Tick(0f);
        blackboard.recoiling = true;
        bind.Tick(0.1f);
        Assert.That(bind.IsBinding, Is.False);

        bind = CreateBind();
        SetInput(true, true);
        bind.Tick(0f);
        blackboard.recoiling = false;
        blackboard.controlLocked = true;
        bind.Tick(0.1f);
        Assert.That(bind.IsBinding, Is.False);

        bind = CreateBind();
        blackboard.controlLocked = false;
        SetInput(true, true);
        bind.Tick(0f);
        blackboard.grounded = false;
        bind.Tick(0.1f);
        Assert.That(bind.IsBinding, Is.False);
    }

    [Test]
    public void SuccessfulCompletionSpendsAndHealsExactlyOnce()
    {
        bind.Tick(0f);
        SetInput(false, true);
        bind.Tick(1f);

        Assert.That(bind.IsBinding, Is.False);
        Assert.That(bind.HasCompleted, Is.True);
        Assert.That(resource.CurrentParts, Is.EqualTo(2));
        Assert.That(health.CurrentHealth, Is.EqualTo(5));

        bind.CompleteFromAnimation();
        Assert.That(resource.CurrentParts, Is.EqualTo(2));
        Assert.That(health.CurrentHealth, Is.EqualTo(5));
    }

    [Test]
    public void CompletionClampsHealingAtMaximum()
    {
        health.ApplySaveData(CreateHealthData(4, 5, 0));
        config.bindHealAmount = 4;
        bind = CreateBind();
        SetInput(true, true);

        bind.Tick(0f);
        SetInput(false, true);
        bind.Tick(1f);

        Assert.That(health.CurrentHealth, Is.EqualTo(5));
        Assert.That(resource.CurrentParts, Is.EqualTo(2));
    }

    [Test]
    public void HealthBecomingFullBeforeCompletionCancelsWithoutSpending()
    {
        bind.Tick(0f);
        health.Heal(1);
        SetInput(false, true);
        bind.Tick(0.1f);

        Assert.That(bind.IsBinding, Is.False);
        Assert.That(resource.CurrentParts, Is.EqualTo(5));
        Assert.That(health.CurrentHealth, Is.EqualTo(5));
    }

    [Test]
    public void MissingDependenciesFailSafely()
    {
        HeroBindAction invalid = new HeroBindAction(
            null,
            blackboard,
            input,
            null,
            health,
            resource,
            abilityState,
            () => true,
            null);

        invalid.Tick(0f);

        Assert.That(invalid.IsBinding, Is.False);
        Assert.That(resource.CurrentParts, Is.EqualTo(5));
    }

    [Test]
    public void AnimationAndTimerCompletionCannotDoubleSpend()
    {
        bind.Tick(0f);
        SetInput(false, true);
        bind.Tick(1f);
        bind.CompleteFromAnimation();
        bind.CompleteFromAnimation();

        Assert.That(resource.CurrentParts, Is.EqualTo(2));
        Assert.That(health.CurrentHealth, Is.EqualTo(5));
    }

    private HeroBindAction CreateBind()
    {
        return new HeroBindAction(config, blackboard, input, null, health, resource, abilityState, () => true, null);
    }

    private void AssertCancelledWithoutMutation()
    {
        Assert.That(bind.IsBinding, Is.False);
        Assert.That(bind.HasCompleted, Is.False);
        Assert.That(resource.CurrentParts, Is.EqualTo(5));
        Assert.That(health.CurrentHealth, Is.EqualTo(4));
        Assert.That(blackboard.binding, Is.False);
    }

    private void SetInput(bool pressed, bool held)
    {
        SetBackingField("<BindPressedThisFrame>k__BackingField", pressed);
        SetBackingField("<BindHeld>k__BackingField", held);
        SetBackingField("<BindReleasedThisFrame>k__BackingField", !held);
    }

    private void SetBackingField(string fieldName, object value)
    {
        input.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(input, value);
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
}
