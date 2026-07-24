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
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] requires PresentationRoot, SpriteRenderer, and Animancer references.");
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] requires ComboFirst, ComboSecond, and ShadowBurst attack controllers.");
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] requires one boss-owned spirit pressure helper.");
        LogAssert.Expect(LogType.Error, "[UndeadExecutionerValidator] Undead Executioner uses explicit attack windows and must not carry EnemyContactDamage.");

        int issues = UndeadExecutionerValidator.ValidateBehaviour(behaviour, false);

        Assert.That(issues, Is.EqualTo(10));
    }
}
