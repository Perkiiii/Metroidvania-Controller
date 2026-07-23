using NUnit.Framework;
using UnityEngine;

public sealed class HeroAttackResultTests
{
    private GameObject enemyObject;
    private EnemyConfig enemyConfig;
    private EnemyStateBlackboard blackboard;
    private EnemyHealthComponent health;

    [SetUp]
    public void SetUp()
    {
        enemyConfig = ScriptableObject.CreateInstance<EnemyConfig>();
        enemyConfig.maxHealth = 3;

        enemyObject = new GameObject("HeroAttackResult Enemy Test");
        blackboard = enemyObject.AddComponent<EnemyStateBlackboard>();
        Rigidbody2D body = enemyObject.AddComponent<Rigidbody2D>();
        health = enemyObject.AddComponent<EnemyHealthComponent>();
        health.Initialize(enemyConfig, blackboard, body, null);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(enemyObject);
        Object.DestroyImmediate(enemyConfig);
    }

    [Test]
    public void AcceptedNonlethalDamageReturnsDamaged()
    {
        HeroAttackResult result = health.ReceiveHeroAttack(CreateHit(1));

        Assert.That(result.Outcome, Is.EqualTo(HeroAttackOutcome.Damaged));
        Assert.That(result.DamageApplied, Is.EqualTo(1));
        Assert.That(result.ResourceEligible, Is.True);
    }

    [Test]
    public void AcceptedLethalDamageReturnsKilledAndDeathFiresOnce()
    {
        int deathCount = 0;
        health.OnDeath += () => deathCount++;

        HeroAttackResult result = health.ReceiveHeroAttack(CreateHit(3));

        Assert.That(result.Outcome, Is.EqualTo(HeroAttackOutcome.Killed));
        Assert.That(result.DamageApplied, Is.EqualTo(3));
        Assert.That(result.ResourceEligible, Is.True);
        Assert.That(deathCount, Is.EqualTo(1));
    }

    [Test]
    public void AlreadyDeadReceiverReturnsIgnored()
    {
        health.ReceiveHeroAttack(CreateHit(3));

        HeroAttackResult result = health.ReceiveHeroAttack(CreateHit(1));

        Assert.That(result.Outcome, Is.EqualTo(HeroAttackOutcome.Ignored));
        Assert.That(result.DamageApplied, Is.Zero);
        Assert.That(result.ResourceEligible, Is.False);
    }

    [Test]
    public void InvalidDamageReturnsIgnored()
    {
        HeroAttackResult result = health.ReceiveHeroAttack(CreateHit(0));

        Assert.That(result.Outcome, Is.EqualTo(HeroAttackOutcome.Ignored));
        Assert.That(result.DamageApplied, Is.Zero);
        Assert.That(result.ResourceEligible, Is.False);
    }

    [Test]
    public void ReservedOutcomesNeverQualifyForResource()
    {
        AssertResultNotEligible(HeroAttackResult.Ignored);
        AssertResultNotEligible(HeroAttackResult.Blocked);
        AssertResultNotEligible(HeroAttackResult.Invulnerable);
    }

    [Test]
    public void ResourceStateRemainsUnchangedByAttackResults()
    {
        PlayerResourceState resource = ScriptableObject.CreateInstance<PlayerResourceState>();
        try
        {
            resource.ApplySaveData(new SaveData
            {
                resource = new ResourceSaveData
                {
                    initialized = true,
                    currentParts = 4,
                    maximumParts = 9
                }
            });

            HeroAttackResult result = health.ReceiveHeroAttack(CreateHit(1));
            HeroAttackResult lethalResult = health.ReceiveHeroAttack(CreateHit(2));

            Assert.That(result.ResourceEligible, Is.True);
            Assert.That(lethalResult.Outcome, Is.EqualTo(HeroAttackOutcome.Killed));
            Assert.That(resource.CurrentParts, Is.EqualTo(4));
            Assert.That(resource.MaximumParts, Is.EqualTo(9));
        }
        finally
        {
            Object.DestroyImmediate(resource);
        }
    }

    [Test]
    public void DamagedFactoryDefaultsToResourceEligible()
    {
        HeroAttackResult result = HeroAttackResult.Damaged(1);

        Assert.That(result.Outcome, Is.EqualTo(HeroAttackOutcome.Damaged));
        Assert.That(result.ResourceEligible, Is.True);
    }

    [Test]
    public void KilledFactoryDefaultsToResourceEligible()
    {
        HeroAttackResult result = HeroAttackResult.Killed(1);

        Assert.That(result.Outcome, Is.EqualTo(HeroAttackOutcome.Killed));
        Assert.That(result.ResourceEligible, Is.True);
    }

    [Test]
    public void DamagedFactoryCanOptOutOfResourceEligibility()
    {
        HeroAttackResult result = HeroAttackResult.Damaged(1, resourceEligible: false);

        Assert.That(result.Outcome, Is.EqualTo(HeroAttackOutcome.Damaged));
        Assert.That(result.ResourceEligible, Is.False);
    }

    [Test]
    public void KilledFactoryCanOptOutOfResourceEligibility()
    {
        HeroAttackResult result = HeroAttackResult.Killed(1, resourceEligible: false);

        Assert.That(result.Outcome, Is.EqualTo(HeroAttackOutcome.Killed));
        Assert.That(result.ResourceEligible, Is.False);
    }

    private static HeroAttackHit CreateHit(int damage)
    {
        return new HeroAttackHit(null, HeroAttackDirection.Side, damage, Vector2.zero, Vector2.right);
    }

    private static void AssertResultNotEligible(HeroAttackResult result)
    {
        Assert.That(result.ResourceEligible, Is.False);
        Assert.That(result.DamageApplied, Is.Zero);
    }
}
