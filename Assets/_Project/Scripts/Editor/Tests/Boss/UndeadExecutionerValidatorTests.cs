using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class UndeadExecutionerValidatorTests
{
    private GameObject root;
    private UndeadExecutionerConfig config;

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        if (config != null) Object.DestroyImmediate(config);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ValidatorReportsNonSpectralRigidbodyAndContactDamage()
    {
        root = new GameObject("Invalid Executioner");
        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.gravityScale = 1f;
        root.AddComponent<BoxCollider2D>();
        root.AddComponent<DamageHero>();
        root.AddComponent<EnemyContactDamage>();
        UndeadExecutionerBehaviour behaviour = root.AddComponent<UndeadExecutionerBehaviour>();

        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] is missing UndeadExecutionerConfig.");
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] requires EnemyMotor.");
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] requires EnemyHealthComponent.");
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] Rigidbody2D gravity scale must be zero.");
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] Rigidbody2D must freeze Y position for stable hover.");
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] Rigidbody2D must freeze rotation.");
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] Damageable body collider must be on the Enemies layer.");
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] requires PresentationRoot, SpriteRenderer, and Animancer references.");
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] requires ComboFirst, ComboSecond, and ShadowBurst attack controllers.");
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] requires one boss-owned spirit pressure helper.");
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] Undead Executioner uses explicit attack windows and must not carry EnemyContactDamage.");

        int issues = UndeadExecutionerValidator.ValidateBehaviour(behaviour, false);

        Assert.That(issues, Is.EqualTo(11));
    }

    [Test]
    public void BodyColliderParentLookupResolvesConfiguredEnemyHealthReceiver()
    {
        root = new GameObject("ActorRoot");
        root.layer = LayerMask.NameToLayer("Enemies");
        EnemyHealthComponent health = root.AddComponent<EnemyHealthComponent>();
        GameObject hurtbox = new GameObject("BodyHurtbox");
        hurtbox.layer = root.layer;
        hurtbox.transform.SetParent(root.transform);
        BoxCollider2D bodyCollider = hurtbox.AddComponent<BoxCollider2D>();

        IHeroAttackReceiver receiver = UndeadExecutionerValidator.ResolveHeroAttackReceiver(bodyCollider);

        Assert.That(receiver, Is.SameAs(health));
    }

    [Test]
    public void SpiritHierarchyCannotContainEncounterParticipant()
    {
        root = new GameObject("Spirit");
        UndeadExecutionerSpiritPressure spirit = root.AddComponent<UndeadExecutionerSpiritPressure>();
        GameObject child = new GameObject("Forbidden Participant");
        child.transform.SetParent(root.transform);
        child.AddComponent<BossEncounterParticipant>();
        LogAssert.Expect(
            LogType.Error,
            "[UndeadExecutionerValidator] Spirit hierarchy must not contain a BossEncounterParticipant.");

        Assert.That(UndeadExecutionerValidator.ValidateSpiritHierarchy(spirit), Is.EqualTo(1));
    }
}
