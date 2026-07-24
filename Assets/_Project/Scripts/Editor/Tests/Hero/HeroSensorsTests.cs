using NUnit.Framework;
using UnityEngine;

public sealed class HeroSensorsTests
{
    private GameObject hero;
    private GameObject ground;
    private GameObject overlappingTrigger;
    private HeroConfig config;

    [TearDown]
    public void TearDown()
    {
        if (hero != null) Object.DestroyImmediate(hero);
        if (ground != null) Object.DestroyImmediate(ground);
        if (overlappingTrigger != null) Object.DestroyImmediate(overlappingTrigger);
        if (config != null) Object.DestroyImmediate(config);
    }

    [Test]
    public void GroundProbeIgnoresOverlappingTriggerAndFindsSolidTerrainBehindIt()
    {
        HeroSensors sensors = CreateHeroSensors();
        CreateOverlappingTrigger();
        CreateGround();
        Physics2D.SyncTransforms();

        sensors.FixedTick();

        Assert.That(sensors.IsGrounded, Is.True);
        Assert.That(hero.GetComponent<HeroStateBlackboard>().grounded, Is.True);
    }

    [Test]
    public void GroundProbeDoesNotTreatOverlappingTriggerAsGround()
    {
        HeroSensors sensors = CreateHeroSensors();
        CreateOverlappingTrigger();
        Physics2D.SyncTransforms();

        sensors.FixedTick();

        Assert.That(sensors.IsGrounded, Is.False);
        Assert.That(hero.GetComponent<HeroStateBlackboard>().grounded, Is.False);
    }

    private HeroSensors CreateHeroSensors()
    {
        config = ScriptableObject.CreateInstance<HeroConfig>();
        config.terrainLayers = 1 << 0;
        config.groundProbeDistance = 0.16f;
        config.sensorInset = 0.02f;

        hero = new GameObject("Hero");
        hero.layer = LayerMask.NameToLayer("Player");
        Rigidbody2D body = hero.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        BoxCollider2D bodyCollider = hero.AddComponent<BoxCollider2D>();
        bodyCollider.size = Vector2.one;
        HeroStateBlackboard blackboard = hero.AddComponent<HeroStateBlackboard>();
        HeroSensors sensors = hero.AddComponent<HeroSensors>();
        sensors.Initialize(config, blackboard, body, bodyCollider);
        return sensors;
    }

    private void CreateGround()
    {
        ground = new GameObject("Terrain");
        ground.layer = 0;
        ground.transform.position = new Vector3(0f, -1f, 0f);
        BoxCollider2D collider = ground.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
    }

    private void CreateOverlappingTrigger()
    {
        overlappingTrigger = new GameObject("Room Volume");
        overlappingTrigger.layer = 0;
        BoxCollider2D collider = overlappingTrigger.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(10f, 10f);
        collider.isTrigger = true;
    }
}
