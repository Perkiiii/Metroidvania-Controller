using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class RainImpactSplashHandlerTests
{
    private readonly System.Collections.Generic.List<Object> createdObjects =
        new System.Collections.Generic.List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void UpwardFacingStaticTerrainRequestsOneReusedSplash()
    {
        Fixture fixture = CreateFixture();
        BoxCollider2D terrain = CreateCollider("Terrain Top", 7);

        bool emitted = InvokeTryEmit(
            fixture.Handler,
            terrain,
            new Vector3(3f, 4f, 0f),
            Vector3.up,
            1f);

        Assert.That(emitted, Is.True);
        Assert.That(fixture.Handler.EmittedSplashCount, Is.EqualTo(1));
        Assert.That(fixture.Splash.particleCount, Is.EqualTo(1));

        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[1];
        Assert.That(fixture.Splash.GetParticles(particles), Is.EqualTo(1));
        Assert.That(particles[0].position.x, Is.EqualTo(3f).Within(0.001f));
        Assert.That(particles[0].position.y, Is.EqualTo(4.03f).Within(0.001f));
        Assert.That(particles[0].position.z, Is.EqualTo(-0.45f).Within(0.001f));
    }

    [Test]
    public void WallsCeilingsWrongLayersAndDynamicBodiesAreRejected()
    {
        Fixture fixture = CreateFixture();
        BoxCollider2D terrain = CreateCollider("Terrain", 7);
        BoxCollider2D wrongLayer = CreateCollider("Not Terrain", 0);
        BoxCollider2D dynamicTerrain = CreateCollider("Dynamic Terrain", 7);
        dynamicTerrain.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;

        Assert.That(InvokeTryEmit(fixture.Handler, terrain, Vector3.zero, Vector3.right, 1f), Is.False);
        Assert.That(InvokeTryEmit(fixture.Handler, terrain, Vector3.zero, Vector3.down, 1f), Is.False);
        Assert.That(InvokeTryEmit(
            fixture.Handler,
            terrain,
            Vector3.zero,
            new Vector3(0.8f, 0.6f, 0f),
            1f), Is.False);
        Assert.That(InvokeTryEmit(fixture.Handler, wrongLayer, Vector3.zero, Vector3.up, 1f), Is.False);
        Assert.That(InvokeTryEmit(fixture.Handler, dynamicTerrain, Vector3.zero, Vector3.up, 1f), Is.False);
        Assert.That(fixture.Handler.EmittedSplashCount, Is.Zero);
        Assert.That(fixture.Splash.particleCount, Is.Zero);
    }

    [Test]
    public void SplashSamplingIsRateLimitedAndDisableRejectsFurtherRequests()
    {
        Fixture fixture = CreateFixture();
        BoxCollider2D terrain = CreateCollider("Terrain", 7);

        Assert.That(InvokeTryEmit(fixture.Handler, terrain, Vector3.zero, Vector3.up, 1f), Is.True);
        Assert.That(InvokeTryEmit(fixture.Handler, terrain, Vector3.one, Vector3.up, 1.01f), Is.False);
        Assert.That(InvokeTryEmit(fixture.Handler, terrain, Vector3.one, Vector3.up, 1.07f), Is.True);

        fixture.Handler.SetImpactsEnabled(false);
        Assert.That(InvokeTryEmit(fixture.Handler, terrain, Vector3.one, Vector3.up, 2f), Is.False);
        Assert.That(fixture.Handler.IsAcceptingImpacts, Is.False);
        Assert.That(fixture.Handler.EmittedSplashCount, Is.EqualTo(2));
    }

    private Fixture CreateFixture()
    {
        GameObject sourceObject = Track(new GameObject("MidRain"));
        ParticleSystem source = sourceObject.AddComponent<ParticleSystem>();
        RainImpactSplashHandler handler = sourceObject.AddComponent<RainImpactSplashHandler>();

        GameObject splashObject = Track(new GameObject("GroundSplashPool"));
        splashObject.transform.position = new Vector3(0f, 0f, -0.45f);
        ParticleSystem splash = splashObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = splash.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        splash.Play(false);

        SetField(handler, "collisionSource", source);
        SetField(handler, "splashSystem", splash);
        SetField(handler, "terrainLayers", (LayerMask)(1 << 7));
        SetField(handler, "minimumUpwardNormalY", 0.65f);
        SetField(handler, "surfaceOffset", 0.03f);
        SetField(handler, "minimumSecondsBetweenSplashes", 0.065f);
        handler.SetImpactsEnabled(true);
        return new Fixture(handler, splash);
    }

    private BoxCollider2D CreateCollider(string name, int layer)
    {
        GameObject gameObject = Track(new GameObject(name));
        gameObject.layer = layer;
        return gameObject.AddComponent<BoxCollider2D>();
    }

    private static bool InvokeTryEmit(
        RainImpactSplashHandler handler,
        Collider2D collider,
        Vector3 intersection,
        Vector3 normal,
        float eventTime)
    {
        MethodInfo method = typeof(RainImpactSplashHandler).GetMethod(
            "TryEmitSplash",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(handler, new object[] { collider, intersection, normal, eventTime });
    }

    private T Track<T>(T value) where T : Object
    {
        createdObjects.Add(value);
        return value;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private readonly struct Fixture
    {
        public readonly RainImpactSplashHandler Handler;
        public readonly ParticleSystem Splash;

        public Fixture(RainImpactSplashHandler handler, ParticleSystem splash)
        {
            Handler = handler;
            Splash = splash;
        }
    }
}
