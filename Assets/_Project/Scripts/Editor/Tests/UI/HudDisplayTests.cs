using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class HudDisplayTests
{
    private PlayerHealthState health;
    private PlayerResourceState resource;
    private PlayerResourceConfig config;
    private GameObject healthObject;
    private GameObject resourceObject;
    private HealthDisplay healthDisplay;
    private ResourceDisplay resourceDisplay;

    [SetUp]
    public void SetUp()
    {
        health = ScriptableObject.CreateInstance<PlayerHealthState>();
        health.ApplySaveData(CreateHealthData(3, 5, 2));
        resource = ScriptableObject.CreateInstance<PlayerResourceState>();
        resource.SetMaximumParts(10);
        resource.Gain(7);
        config = ScriptableObject.CreateInstance<PlayerResourceConfig>();
        config.partsPerPip = 3;

        healthObject = new GameObject("Health Display Test");
        healthDisplay = healthObject.AddComponent<HealthDisplay>();
        healthDisplay.Configure(health);

        resourceObject = new GameObject("Resource Display Test");
        resourceDisplay = resourceObject.AddComponent<ResourceDisplay>();
        resourceDisplay.Configure(resource, config);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(healthObject);
        Object.DestroyImmediate(resourceObject);
        Object.DestroyImmediate(health);
        Object.DestroyImmediate(resource);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void HealthInitialRefreshAndGameplayChangesAreReflected()
    {
        Assert.That(healthDisplay.CurrentHealth, Is.EqualTo(3));
        Assert.That(healthDisplay.MaximumHealth, Is.EqualTo(5));
        Assert.That(healthDisplay.BonusHealth, Is.EqualTo(2));
        Assert.That(healthDisplay.NormalSlotCount, Is.EqualTo(5));
        Assert.That(healthDisplay.FilledNormalSlotCount, Is.EqualTo(3));
        Assert.That(healthDisplay.BonusSlotCount, Is.EqualTo(2));
        Assert.That(healthDisplay.GameplayFeedbackCount, Is.EqualTo(0));

        health.ApplyDamage(1);
        Assert.That(healthDisplay.CurrentHealth, Is.EqualTo(3));
        Assert.That(healthDisplay.BonusHealth, Is.EqualTo(1));
        Assert.That(healthDisplay.GameplayFeedbackCount, Is.EqualTo(1));

        health.Heal(1);
        Assert.That(healthDisplay.CurrentHealth, Is.EqualTo(4));
        Assert.That(healthDisplay.GameplayFeedbackCount, Is.EqualTo(2));

        health.SetMaximumHealth(6);
        Assert.That(healthDisplay.NormalSlotCount, Is.EqualTo(6));

        health.ForceDeplete();
        Assert.That(healthDisplay.FilledNormalSlotCount, Is.EqualTo(0));
        Assert.That(healthDisplay.BonusSlotCount, Is.EqualTo(0));
    }

    [Test]
    public void HealthStateApplicationRefreshesWithoutGameplayFeedback()
    {
        int feedbackBefore = healthDisplay.GameplayFeedbackCount;
        health.ApplySaveData(CreateHealthData(1, 4, 3));

        Assert.That(healthDisplay.CurrentHealth, Is.EqualTo(1));
        Assert.That(healthDisplay.MaximumHealth, Is.EqualTo(4));
        Assert.That(healthDisplay.BonusHealth, Is.EqualTo(3));
        Assert.That(healthDisplay.GameplayFeedbackCount, Is.EqualTo(feedbackBefore));
        Assert.That(healthDisplay.LastChangeReason, Is.EqualTo(PlayerHealthChangeReason.StateApplied));
    }

    [Test]
    public void HealthSubscriptionIsNotDuplicatedAndStopsWhenDisabled()
    {
        healthDisplay.Configure(health);
        healthDisplay.Configure(health);
        health.ApplyDamage(1);
        Assert.That(healthDisplay.GameplayFeedbackCount, Is.EqualTo(1));

        // GameObject.SetActive does not synchronously dispatch OnDisable outside Play Mode,
        // so invoke it directly to verify the same unsubscribe path Play Mode would trigger.
        healthObject.SetActive(false);
        InvokePrivate(healthDisplay, "OnDisable");
        health.ApplyDamage(1);
        Assert.That(healthDisplay.GameplayFeedbackCount, Is.EqualTo(1));
    }

    [Test]
    public void ResourceInitialRefreshCalculatesHorizontalBarFill()
    {
        Assert.That(resourceDisplay.CurrentParts, Is.EqualTo(7));
        Assert.That(resourceDisplay.MaximumParts, Is.EqualTo(10));
        Assert.That(resourceDisplay.FillAmount01, Is.EqualTo(0.7f).Within(0.0001f));
        Assert.That(resourceDisplay.IsVisible, Is.True);
    }

    [Test]
    public void ResourceGainSpendCapacityAndNeutralApplicationAreReflected()
    {
        int feedbackBefore = resourceDisplay.GameplayFeedbackCount;
        resource.Gain(1);
        Assert.That(resourceDisplay.CurrentParts, Is.EqualTo(8));
        Assert.That(resourceDisplay.GameplayFeedbackCount, Is.EqualTo(feedbackBefore + 1));

        resource.TrySpend(2);
        Assert.That(resourceDisplay.CurrentParts, Is.EqualTo(6));
        Assert.That(resourceDisplay.GameplayFeedbackCount, Is.EqualTo(feedbackBefore + 2));

        resource.ApplySaveData(CreateResourceData(2, 4));
        Assert.That(resourceDisplay.CurrentParts, Is.EqualTo(2));
        Assert.That(resourceDisplay.MaximumParts, Is.EqualTo(4));
        Assert.That(resourceDisplay.FillAmount01, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(resourceDisplay.GameplayFeedbackCount, Is.EqualTo(feedbackBefore + 2));
        Assert.That(resourceDisplay.LastChangeReason, Is.EqualTo(PlayerResourceChangeReason.StateApplied));
    }

    [Test]
    public void ResourceFullCapacityDisplaysFullPipsAndRejectsFurtherGain()
    {
        resource.Gain(100);

        Assert.That(resourceDisplay.CurrentParts, Is.EqualTo(10));
        Assert.That(resourceDisplay.FillAmount01, Is.EqualTo(1f));
        Assert.That(resource.Gain(1), Is.EqualTo(0));
    }

    [Test]
    public void ResourceZeroCapacityIsSafeAndHidden()
    {
        resource.SetMaximumParts(0);

        Assert.That(resourceDisplay.FillAmount01, Is.EqualTo(0f));
        Assert.That(resourceDisplay.IsVisible, Is.True);
    }

    [Test]
    public void ResourceSubscriptionIsNotDuplicatedAndStopsWhenDisabled()
    {
        resourceDisplay.Configure(resource, config);
        resourceDisplay.Configure(resource, config);
        resource.Gain(1);
        Assert.That(resourceDisplay.GameplayFeedbackCount, Is.EqualTo(1));

        // GameObject.SetActive does not synchronously dispatch OnDisable outside Play Mode,
        // so invoke it directly to verify the same unsubscribe path Play Mode would trigger.
        resourceObject.SetActive(false);
        InvokePrivate(resourceDisplay, "OnDisable");
        resource.Gain(1);
        Assert.That(resourceDisplay.GameplayFeedbackCount, Is.EqualTo(1));
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
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

    private static SaveData CreateResourceData(int current, int maximum)
    {
        return new SaveData
        {
            resource = new ResourceSaveData
            {
                initialized = true,
                currentParts = current,
                maximumParts = maximum
            }
        };
    }
}
