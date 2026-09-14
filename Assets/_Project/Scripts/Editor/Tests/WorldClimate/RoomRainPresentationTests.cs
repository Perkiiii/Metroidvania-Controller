using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class RoomRainPresentationTests
{
    private readonly System.Collections.Generic.List<UnityEngine.Object> createdObjects =
        new System.Collections.Generic.List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        if (AudioManager.Instance != null)
        {
            InvokePrivate(AudioManager.Instance, "OnDestroy");
        }

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
        CameraInfoCache.UpdateCache(null, true);
    }

    [Test]
    public void RainAndStormRequestsDriveAllThreeVisualLayers()
    {
        Fixture fixture = CreateFixture();
        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Rain);

        Assert.That(fixture.Rain.IsRainRequested, Is.True);
        Assert.That(fixture.Rain.CurrentRequest, Is.EqualTo(RoomWeatherPresentationMode.Rain));
        Assert.That(fixture.Rain.VisualLayerCount, Is.EqualTo(3));
        Assert.That(fixture.Back.emission.rateOverTime.constant, Is.GreaterThan(0f));
        Assert.That(fixture.Mid.emission.rateOverTime.constant, Is.GreaterThan(0f));
        Assert.That(fixture.Front.emission.rateOverTime.constant, Is.GreaterThan(0f));
        Assert.That(fixture.Back.emission.rateOverTime.constant,
            Is.LessThan(fixture.Mid.emission.rateOverTime.constant));
        Assert.That(fixture.Front.emission.rateOverTime.constant,
            Is.LessThan(fixture.Back.emission.rateOverTime.constant));

        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Storm);

        Assert.That(fixture.Rain.IsRainRequested, Is.True);
        Assert.That(fixture.Rain.CurrentRequest, Is.EqualTo(RoomWeatherPresentationMode.Storm));
        Assert.That(fixture.ImpactHandler.IsAcceptingImpacts, Is.True);
    }

    [Test]
    public void ClearStopsAllRainEmissionSplashRequestsAndAmbience()
    {
        Fixture fixture = CreateFixture(withAudio: true);
        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Rain);
        Assert.That(fixture.Audio.CurrentAmbienceClip, Is.SameAs(fixture.AmbienceClip));

        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Clear);

        Assert.That(fixture.Rain.IsRainRequested, Is.False);
        Assert.That(fixture.Back.emission.rateOverTime.constant, Is.Zero);
        Assert.That(fixture.Mid.emission.rateOverTime.constant, Is.Zero);
        Assert.That(fixture.Front.emission.rateOverTime.constant, Is.Zero);
        Assert.That(fixture.Splash.emission.enabled, Is.False);
        Assert.That(fixture.Splash.particleCount, Is.Zero);
        Assert.That(fixture.ImpactHandler.IsAcceptingImpacts, Is.False);
        Assert.That(fixture.Audio.CurrentAmbienceClip, Is.Null);
        Assert.That(fixture.Audio.IsAmbienceActive, Is.False);
    }

    [Test]
    public void StalePresenterClearCannotStopAReplacementAmbienceRequest()
    {
        Fixture first = CreateFixture(withAudio: true);
        InvokePrivate(first.Weather, "SetPresentation", RoomWeatherPresentationMode.Rain);

        Fixture replacement = CreateFixture();
        SetField(replacement.Rain, "rainAmbienceClip", first.AmbienceClip);
        InvokePrivate(replacement.Weather, "SetPresentation", RoomWeatherPresentationMode.Rain);

        InvokePrivate(first.Rain, "OnDisable");

        Assert.That(first.Audio.CurrentAmbienceClip, Is.SameAs(first.AmbienceClip));
        Assert.That(first.Audio.IsAmbienceActive, Is.True);
        Assert.That(first.Audio.AmbienceTargetVolume, Is.EqualTo(0.28f).Within(0.001f));

        InvokePrivate(replacement.Weather, "SetPresentation", RoomWeatherPresentationMode.Clear);
        Assert.That(first.Audio.CurrentAmbienceClip, Is.Null);
        Assert.That(first.Audio.IsAmbienceActive, Is.False);
    }

    [Test]
    public void RepeatedRainClearCyclesKeepOneListenerAndCleanAllLayers()
    {
        Fixture fixture = CreateFixture();
        for (int i = 0; i < 8; i++)
        {
            InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Rain);
            InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Storm);
            InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Clear);
        }

        Assert.That(GetSubscriberCount(fixture.Weather), Is.EqualTo(1));
        Assert.That(fixture.Rain.IsRainRequested, Is.False);
        Assert.That(fixture.Back.emission.rateOverTime.constant, Is.Zero);
        Assert.That(fixture.Mid.emission.rateOverTime.constant, Is.Zero);
        Assert.That(fixture.Front.emission.rateOverTime.constant, Is.Zero);
        Assert.That(fixture.ImpactHandler.IsAcceptingImpacts, Is.False);
    }

    [Test]
    public void OnlyMidLayerUsesStaticTerrainCollisionAndMessages()
    {
        Fixture fixture = CreateFixture();
        ParticleSystem.CollisionModule backCollision = fixture.Back.collision;
        ParticleSystem.CollisionModule midCollision = fixture.Mid.collision;
        ParticleSystem.CollisionModule frontCollision = fixture.Front.collision;

        Assert.That(fixture.Back.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(fixture.Mid.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(fixture.Front.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(fixture.Back.velocityOverLifetime.space,
            Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(fixture.Mid.velocityOverLifetime.space,
            Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(fixture.Front.velocityOverLifetime.space,
            Is.EqualTo(ParticleSystemSimulationSpace.World));

        Assert.That(backCollision.enabled, Is.False);
        Assert.That(backCollision.sendCollisionMessages, Is.False);
        Assert.That(frontCollision.enabled, Is.False);
        Assert.That(frontCollision.sendCollisionMessages, Is.False);

        Assert.That(midCollision.enabled, Is.True);
        Assert.That(midCollision.type, Is.EqualTo(ParticleSystemCollisionType.World));
        Assert.That(midCollision.mode, Is.EqualTo(ParticleSystemCollisionMode.Collision2D));
        Assert.That(midCollision.collidesWith.value, Is.EqualTo(1 << 7));
        Assert.That(midCollision.enableDynamicColliders, Is.False);
        Assert.That(midCollision.sendCollisionMessages, Is.True);
        Assert.That(midCollision.lifetimeLoss.constant, Is.EqualTo(1f));
        Assert.That(fixture.ImpactHandler.CollisionSource, Is.SameAs(fixture.Mid));
    }

    [Test]
    public void PerspectiveCoverageUsesDistinctDepthsAndFrustumWidths()
    {
        Fixture fixture = CreateFixture();
        GameObject cameraObject = Track(new GameObject("Rain Coverage Camera"));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(4f, 2f, -38.1f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 24f;
        camera.aspect = 16f / 9f;
        CameraInfoCache.UpdateCache(camera, true);

        InvokePrivate(fixture.Rain, "LateUpdate");

        Assert.That(fixture.Back.transform.position.z, Is.EqualTo(5.9f).Within(0.001f));
        Assert.That(fixture.Mid.transform.position.z, Is.EqualTo(-0.3f).Within(0.001f));
        Assert.That(fixture.Front.transform.position.z, Is.EqualTo(-8.6f).Within(0.001f));
        Assert.That(fixture.Splash.transform.position.z, Is.EqualTo(-0.45f).Within(0.001f));
        Assert.That(fixture.Back.shape.scale.x, Is.GreaterThan(fixture.Mid.shape.scale.x));
        Assert.That(fixture.Mid.shape.scale.x, Is.GreaterThan(fixture.Front.shape.scale.x));
        Assert.That(fixture.Back.shape.scale.z, Is.EqualTo(0.35f).Within(0.001f));
        Assert.That(fixture.Mid.shape.scale.z, Is.EqualTo(0.4f).Within(0.001f));
        Assert.That(fixture.Front.shape.scale.z, Is.EqualTo(0.3f).Within(0.001f));
    }

    [Test]
    public void RainPresentationDoesNotOwnAudioSources()
    {
        Fixture fixture = CreateFixture();
        Assert.That(fixture.Root.GetComponentsInChildren<AudioSource>(true), Is.Empty);
    }

    [Test]
    public void DisableUnsubscribesAndLeavesNoActiveSplashRequests()
    {
        Fixture fixture = CreateFixture();
        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Rain);
        InvokePrivate(fixture.Rain, "OnDisable");
        InvokePrivate(fixture.Rain, "OnDisable");

        Assert.That(GetSubscriberCount(fixture.Weather), Is.Zero);
        Assert.That(fixture.Rain.IsRainRequested, Is.False);
        Assert.That(fixture.Back.emission.rateOverTime.constant, Is.Zero);
        Assert.That(fixture.Mid.emission.rateOverTime.constant, Is.Zero);
        Assert.That(fixture.Front.emission.rateOverTime.constant, Is.Zero);
        Assert.That(fixture.ImpactHandler.IsAcceptingImpacts, Is.False);
        Assert.That(fixture.Splash.particleCount, Is.Zero);
    }

    private Fixture CreateFixture(bool withAudio = false)
    {
        GameObject root = Track(new GameObject("Room Rain Presentation Test"));
        root.SetActive(false);
        RoomWeatherPresentation weather = root.AddComponent<RoomWeatherPresentation>();
        RoomRainPresentation rain = root.AddComponent<RoomRainPresentation>();
        ParticleSystem back = CreateParticle(root.transform, "BackRain");
        ParticleSystem mid = CreateParticle(root.transform, "MidRain");
        ParticleSystem front = CreateParticle(root.transform, "FrontRain");
        ParticleSystem splash = CreateParticle(root.transform, "GroundSplashPool");
        RainImpactSplashHandler handler = mid.gameObject.AddComponent<RainImpactSplashHandler>();

        SetField(handler, "collisionSource", mid);
        SetField(handler, "splashSystem", splash);
        SetField(handler, "terrainLayers", (LayerMask)(1 << 7));
        SetField(rain, "weatherPresentation", weather);
        SetField(rain, "backRainLayer", back);
        SetField(rain, "midRainLayer", mid);
        SetField(rain, "frontRainLayer", front);
        SetField(rain, "splashSystem", splash);
        SetField(rain, "impactSplashHandler", handler);
        SetField(rain, "terrainCollisionLayers", (LayerMask)(1 << 7));
        SetField(rain, "rampSeconds", 0f);
        SetField(rain, "ambienceFadeSeconds", 0f);

        AudioManager audio = null;
        AudioClip ambienceClip = null;
        if (withAudio)
        {
            if (AudioManager.Instance != null)
            {
                InvokePrivate(AudioManager.Instance, "OnDestroy");
            }

            GameObject audioObject = Track(new GameObject("Audio Manager Test"));
            AudioSource sfx = audioObject.AddComponent<AudioSource>();
            AudioSource music = audioObject.AddComponent<AudioSource>();
            AudioSource ambience = audioObject.AddComponent<AudioSource>();
            sfx.gameObject.name = "SFX";
            music.gameObject.name = "Music";
            ambience.gameObject.name = "Ambience";
            audio = audioObject.AddComponent<AudioManager>();
            SetField(audio, "sfxSource", sfx);
            SetField(audio, "musicSource", music);
            SetField(audio, "ambienceSource", ambience);
            ambienceClip = Track(AudioClip.Create("Rain Test Ambience", 16, 1, 44100, false));
            SetField(rain, "rainAmbienceClip", ambienceClip);
            InvokePrivate(audio, "Awake");
        }

        InvokePrivate(weather, "OnEnable");
        InvokePrivate(rain, "OnEnable");
        return new Fixture(root, weather, rain, back, mid, front, splash, handler, audio, ambienceClip);
    }

    private ParticleSystem CreateParticle(Transform parent, string name)
    {
        GameObject particleObject = Track(new GameObject(name));
        particleObject.transform.SetParent(parent, false);
        return particleObject.AddComponent<ParticleSystem>();
    }

    private static int GetSubscriberCount(RoomWeatherPresentation weather)
    {
        FieldInfo field = typeof(RoomWeatherPresentation).GetField(
            "PresentationChanged",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Delegate callback = field.GetValue(weather) as Delegate;
        return callback == null ? 0 : callback.GetInvocationList().Length;
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        createdObjects.Add(value);
        return value;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' on '{target.GetType().Name}'.");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Expected method '{methodName}' on '{target.GetType().Name}'.");
        method.Invoke(target, arguments);
    }

    private readonly struct Fixture
    {
        public readonly GameObject Root;
        public readonly RoomWeatherPresentation Weather;
        public readonly RoomRainPresentation Rain;
        public readonly ParticleSystem Back;
        public readonly ParticleSystem Mid;
        public readonly ParticleSystem Front;
        public readonly ParticleSystem Splash;
        public readonly RainImpactSplashHandler ImpactHandler;
        public readonly AudioManager Audio;
        public readonly AudioClip AmbienceClip;

        public Fixture(
            GameObject root,
            RoomWeatherPresentation weather,
            RoomRainPresentation rain,
            ParticleSystem back,
            ParticleSystem mid,
            ParticleSystem front,
            ParticleSystem splash,
            RainImpactSplashHandler impactHandler,
            AudioManager audio,
            AudioClip ambienceClip)
        {
            Root = root;
            Weather = weather;
            Rain = rain;
            Back = back;
            Mid = mid;
            Front = front;
            Splash = splash;
            ImpactHandler = impactHandler;
            Audio = audio;
            AmbienceClip = ambienceClip;
        }
    }
}
