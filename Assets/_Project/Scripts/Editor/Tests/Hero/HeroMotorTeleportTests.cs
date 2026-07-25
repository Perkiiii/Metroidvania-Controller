using NUnit.Framework;
using UnityEngine;

public sealed class HeroMotorTeleportTests
{
    private GameObject hero;

    [TearDown]
    public void TearDown()
    {
        if (hero != null) Object.DestroyImmediate(hero);
    }

    [Test]
    public void TeleportToUpdatesTransformImmediatelyForSameFrameReaders()
    {
        hero = new GameObject("TeleportHero");
        hero.transform.position = new Vector3(1f, 2f, -3f);
        Rigidbody2D body = hero.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        HeroMotor motor = hero.AddComponent<HeroMotor>();
        motor.Initialize(null, null, null, body, null, null, hero.transform);

        Vector2 target = new Vector2(42f, 17f);
        motor.TeleportTo(target);

        // Regression guard for the scene-entry reveal defect: setting Rigidbody2D.position alone does
        // not propagate to the Transform until the next physics step, so same-frame readers (the
        // scene-entry camera readiness that runs immediately after gate placement, behind the black
        // screen) would otherwise see the stale pre-teleport position and frame the wrong spot.
        Assert.That((Vector2)hero.transform.position, Is.EqualTo(target));
        Assert.That(hero.transform.position.z, Is.EqualTo(-3f), "Teleport must preserve the Transform z depth.");
    }
}
