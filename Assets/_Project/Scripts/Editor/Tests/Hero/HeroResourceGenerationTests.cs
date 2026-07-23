using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class HeroResourceGenerationTests
{
    private readonly List<GameObject> targets = new List<GameObject>();
    private GameObject heroObject;
    private HeroConfig config;
    private PlayerResourceState resourceState;
    private HeroStateBlackboard blackboard;
    private HeroAttackModule module;
    private PolygonCollider2D damageCollider;
    private HeroAttackAction action;

    [SetUp]
    public void SetUp()
    {
        config = ScriptableObject.CreateInstance<HeroConfig>();
        config.attackDamage = 1;
        config.maxHitsPerSwing = 16;
        config.attackHitLayers = ~0;

        resourceState = ScriptableObject.CreateInstance<PlayerResourceState>();
        resourceState.SetMaximumParts(20);

        heroObject = new GameObject("Hero Resource Generation Test");
        blackboard = heroObject.AddComponent<HeroStateBlackboard>();
        HeroInputReader input = heroObject.AddComponent<HeroInputReader>();

        GameObject moduleObject = new GameObject("Test Attack Module");
        moduleObject.transform.SetParent(heroObject.transform);
        module = moduleObject.AddComponent<HeroAttackModule>();
        damageCollider = moduleObject.AddComponent<PolygonCollider2D>();
        damageCollider.pathCount = 1;
        damageCollider.SetPath(0, CreateBoxPath(Vector2.one));
        module.damageCollider = damageCollider;
        module.damageLayers = ~0;

        action = new HeroAttackAction(
            config,
            blackboard,
            input,
            null,
            null,
            heroObject,
            heroObject.transform,
            new[] { module },
            1f,
            resourceState);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            Object.DestroyImmediate(targets[i]);
        }

        targets.Clear();
        Object.DestroyImmediate(heroObject);
        Object.DestroyImmediate(resourceState);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void NoneNeverAwardsResource()
    {
        module.resourceGenerationMode = HeroResourceGenerationMode.None;
        module.resourceGainParts = 3;
        CreateTarget(HeroAttackResult.Damaged(1), colliderCount: 1);

        StartAttackAndEvaluate();

        Assert.That(resourceState.CurrentParts, Is.Zero);
    }

    [Test]
    public void PerSuccessfulTargetAwardsOnceForOneAcceptedTarget()
    {
        module.resourceGenerationMode = HeroResourceGenerationMode.PerSuccessfulTarget;
        module.resourceGainParts = 1;
        HeroAttackTestReceiver receiver = CreateTarget(HeroAttackResult.Damaged(1), colliderCount: 1);

        StartAttackAndEvaluate();

        Assert.That(receiver.ReceiveCount, Is.EqualTo(1));
        Assert.That(resourceState.CurrentParts, Is.EqualTo(1));
    }

    [Test]
    public void PerSuccessfulTargetAwardsSeparatelyForDistinctTargets()
    {
        module.resourceGenerationMode = HeroResourceGenerationMode.PerSuccessfulTarget;
        module.resourceGainParts = 1;
        CreateTarget(HeroAttackResult.Damaged(1), colliderCount: 1);
        CreateTarget(HeroAttackResult.Damaged(1), colliderCount: 1);

        StartAttackAndEvaluate();

        Assert.That(resourceState.CurrentParts, Is.EqualTo(2));
    }

    [Test]
    public void MultipleCollidersOnOneReceiverAwardOnlyOnce()
    {
        module.resourceGenerationMode = HeroResourceGenerationMode.PerSuccessfulTarget;
        module.resourceGainParts = 1;
        HeroAttackTestReceiver receiver = CreateTarget(HeroAttackResult.Damaged(1), colliderCount: 2);

        StartAttackAndEvaluate();

        Assert.That(receiver.ReceiveCount, Is.EqualTo(1));
        Assert.That(resourceState.CurrentParts, Is.EqualTo(1));
    }

    [Test]
    public void FirstSuccessfulHitAwardsOnceAcrossMultipleTargets()
    {
        module.resourceGenerationMode = HeroResourceGenerationMode.FirstSuccessfulHitPerAttack;
        module.resourceGainParts = 1;
        CreateTarget(HeroAttackResult.Damaged(1), colliderCount: 1);
        CreateTarget(HeroAttackResult.Killed(1), colliderCount: 1);

        StartAttackAndEvaluate();

        Assert.That(resourceState.CurrentParts, Is.EqualTo(1));
    }

    [Test]
    public void FirstSuccessfulHitResetsForNextAttack()
    {
        module.resourceGenerationMode = HeroResourceGenerationMode.FirstSuccessfulHitPerAttack;
        module.resourceGainParts = 1;
        HeroAttackTestReceiver receiver = CreateTarget(HeroAttackResult.Damaged(1), colliderCount: 1);

        StartAttackAndEvaluate();
        InvokePrivate(action, "EndAttack", false);
        StartAttackAndEvaluate();

        Assert.That(receiver.ReceiveCount, Is.EqualTo(2));
        Assert.That(resourceState.CurrentParts, Is.EqualTo(2));
    }

    [TestCase(HeroAttackOutcome.Ignored)]
    [TestCase(HeroAttackOutcome.Blocked)]
    [TestCase(HeroAttackOutcome.Invulnerable)]
    public void RejectedOutcomesAwardNothing(HeroAttackOutcome outcome)
    {
        module.resourceGenerationMode = HeroResourceGenerationMode.PerSuccessfulTarget;
        module.resourceGainParts = 1;
        CreateTarget(CreateResult(outcome), colliderCount: 1);

        StartAttackAndEvaluate();

        Assert.That(resourceState.CurrentParts, Is.Zero);
    }

    [Test]
    public void DamagedAndKilledResultsMayAward()
    {
        module.resourceGenerationMode = HeroResourceGenerationMode.PerSuccessfulTarget;
        module.resourceGainParts = 1;
        CreateTarget(HeroAttackResult.Damaged(1), colliderCount: 1);
        CreateTarget(HeroAttackResult.Killed(1), colliderCount: 1);

        StartAttackAndEvaluate();

        Assert.That(resourceState.CurrentParts, Is.EqualTo(2));
    }

    [Test]
    public void CapacityClampsGain()
    {
        resourceState.SetMaximumParts(1);
        module.resourceGenerationMode = HeroResourceGenerationMode.PerSuccessfulTarget;
        module.resourceGainParts = 3;
        CreateTarget(HeroAttackResult.Damaged(1), colliderCount: 1);

        StartAttackAndEvaluate();

        Assert.That(resourceState.CurrentParts, Is.EqualTo(1));
    }

    [Test]
    public void LoadedNonZeroCapacityAllowsConfiguredAcceptedHit()
    {
        resourceState.ApplySaveData(new SaveData
        {
            resource = new ResourceSaveData
            {
                initialized = true,
                currentParts = 0,
                maximumParts = 3
            }
        });
        module.resourceGenerationMode = HeroResourceGenerationMode.PerSuccessfulTarget;
        module.resourceGainParts = 1;
        CreateTarget(HeroAttackResult.Damaged(1), colliderCount: 1);

        StartAttackAndEvaluate();

        Assert.That(resourceState.CurrentParts, Is.EqualTo(1));
        Assert.That(resourceState.MaximumParts, Is.EqualTo(3));
    }

    [Test]
    public void LoadedZeroCapacityIntentionallyPreventsGain()
    {
        resourceState.ApplySaveData(new SaveData
        {
            resource = new ResourceSaveData
            {
                initialized = true,
                currentParts = 0,
                maximumParts = 0
            }
        });
        module.resourceGenerationMode = HeroResourceGenerationMode.PerSuccessfulTarget;
        module.resourceGainParts = 1;
        CreateTarget(HeroAttackResult.Damaged(1), colliderCount: 1);

        StartAttackAndEvaluate();

        Assert.That(resourceState.CurrentParts, Is.Zero);
        Assert.That(resourceState.MaximumParts, Is.Zero);
    }

    [Test]
    public void FullCapacityProducesNoIncrease()
    {
        resourceState.Gain(resourceState.MaximumParts);
        module.resourceGenerationMode = HeroResourceGenerationMode.PerSuccessfulTarget;
        module.resourceGainParts = 1;
        CreateTarget(HeroAttackResult.Damaged(1), colliderCount: 1);

        StartAttackAndEvaluate();

        Assert.That(resourceState.CurrentParts, Is.EqualTo(resourceState.MaximumParts));
    }

    [Test]
    public void ZeroOrInvalidGainProducesNoIncrease()
    {
        module.resourceGenerationMode = HeroResourceGenerationMode.PerSuccessfulTarget;
        module.resourceGainParts = 0;
        CreateTarget(HeroAttackResult.Damaged(1), colliderCount: 1);

        StartAttackAndEvaluate();

        Assert.That(resourceState.CurrentParts, Is.Zero);
    }

    [Test]
    public void ClashPathDoesNotAwardResource()
    {
        module.resourceGenerationMode = HeroResourceGenerationMode.PerSuccessfulTarget;
        module.resourceGainParts = 1;
        module.clashCollider = module.gameObject.AddComponent<PolygonCollider2D>();
        module.clashCollider.pathCount = 1;
        module.clashCollider.SetPath(0, CreateBoxPath(Vector2.one));
        HeroAttackClashTestReceiver receiver = CreateClashTarget();

        StartAttackAndEvaluateClash();

        Assert.That(receiver.ReceiveCount, Is.EqualTo(1));
        Assert.That(resourceState.CurrentParts, Is.Zero);
    }

    [Test]
    public void DownslashResponderDoesNotAwardResourceWithoutAcceptedDamage()
    {
        module.resourceGenerationMode = HeroResourceGenerationMode.PerSuccessfulTarget;
        module.resourceGainParts = 1;
        HeroAttackTestReceiver receiver = CreateTarget(HeroAttackResult.Ignored, colliderCount: 1);

        InvokePrivate(action, "StartAttack");
        SetPrivateField(action, "currentDirection", HeroAttackDirection.Down);
        damageCollider.enabled = true;
        Physics2D.SyncTransforms();
        InvokePrivate(action, "EvaluateDamageCollider");

        Assert.That(receiver.DownslashCount, Is.EqualTo(1));
        Assert.That(resourceState.CurrentParts, Is.Zero);
    }

    private void StartAttackAndEvaluate()
    {
        InvokePrivate(action, "StartAttack");
        damageCollider.enabled = true;
        Physics2D.SyncTransforms();
        InvokePrivate(action, "EvaluateDamageCollider");
    }

    private void StartAttackAndEvaluateClash()
    {
        InvokePrivate(action, "StartAttack");
        module.clashCollider.enabled = true;
        Physics2D.SyncTransforms();
        InvokePrivate(action, "EvaluateClashCollider");
    }

    private HeroAttackTestReceiver CreateTarget(HeroAttackResult result, int colliderCount)
    {
        GameObject target = new GameObject("Resource Target");
        targets.Add(target);
        HeroAttackTestReceiver receiver = target.AddComponent<HeroAttackTestReceiver>();
        receiver.Result = result;

        for (int i = 0; i < colliderCount; i++)
        {
            GameObject hurtbox = new GameObject("Hurtbox");
            hurtbox.transform.SetParent(target.transform);
            BoxCollider2D collider = hurtbox.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
        }

        return receiver;
    }

    private HeroAttackClashTestReceiver CreateClashTarget()
    {
        GameObject target = new GameObject("Clash Target");
        targets.Add(target);
        HeroAttackClashTestReceiver receiver = target.AddComponent<HeroAttackClashTestReceiver>();
        GameObject hurtbox = new GameObject("Clash Hurtbox");
        hurtbox.transform.SetParent(target.transform);
        hurtbox.AddComponent<BoxCollider2D>();
        return receiver;
    }

    private static HeroAttackResult CreateResult(HeroAttackOutcome outcome)
    {
        switch (outcome)
        {
            case HeroAttackOutcome.Blocked:
                return HeroAttackResult.Blocked;
            case HeroAttackOutcome.Invulnerable:
                return HeroAttackResult.Invulnerable;
            default:
                return HeroAttackResult.Ignored;
        }
    }

    private static Vector2[] CreateBoxPath(Vector2 size)
    {
        Vector2 halfSize = size * 0.5f;
        return new[]
        {
            new Vector2(-halfSize.x, -halfSize.y),
            new Vector2(-halfSize.x, halfSize.y),
            new Vector2(halfSize.x, halfSize.y),
            new Vector2(halfSize.x, -halfSize.y)
        };
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}

public sealed class HeroAttackTestReceiver : MonoBehaviour, IHeroAttackReceiver, IHeroDownslashResponder
{
    public HeroAttackResult Result { get; set; } = HeroAttackResult.Ignored;
    public int ReceiveCount { get; private set; }
    public int DownslashCount { get; private set; }

    public HeroAttackResult ReceiveHeroAttack(HeroAttackHit hit)
    {
        ReceiveCount++;
        return Result;
    }

    public void ReceiveHeroDownslash(HeroAttackHit hit)
    {
        DownslashCount++;
    }
}

public sealed class HeroAttackClashTestReceiver : MonoBehaviour, IHeroAttackClashReceiver
{
    public int ReceiveCount { get; private set; }

    public void ReceiveHeroAttackClash(HeroAttackHit hit)
    {
        ReceiveCount++;
    }
}
