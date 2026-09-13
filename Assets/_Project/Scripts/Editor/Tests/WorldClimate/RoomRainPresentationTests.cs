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
    public void RainAndStormRequestsEnableTheSameRainLayer()
    {
        Fixture fixture = CreateFixture();
        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Rain);

        Assert.That(fixture.Rain.IsRainRequested, Is.True);
        Assert.That(fixture.Rain.CurrentRequest, Is.EqualTo(RoomWeatherPresentationMode.Rain));
        Assert.That(fixture.Rain.RainLayer.emission.rateOverTime.constant, Is.GreaterThan(0f));

        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Storm);

        Assert.That(fixture.Rain.IsRainRequested, Is.True);
        Assert.That(fixture.Rain.CurrentRequest, Is.EqualTo(RoomWeatherPresentationMode.Storm));
        Assert.That(fixture.Rain.SplashEmitterCount, Is.EqualTo(3));
    }

    [Test]
    public void ClearStopsNewRainAndSplashEmissionAndCleansAmbience()
    {
        Fixture fixture = CreateFixture(withAudio: true);
        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Rain);
        Assert.That(fixture.Audio.CurrentAmbienceClip, Is.SameAs(fixture.AmbienceClip));

        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Clear);

        Assert.That(fixture.Rain.IsRainRequested, Is.False);
        Assert.That(fixture.Rain.RainLayer.emission.rateOverTime.constant, Is.EqualTo(0f));
        for (int i = 0; i < fixture.Splashes.Length; i++)
        {
            Assert.That(fixture.Splashes[i].emission.rateOverTime.constant, Is.EqualTo(0f));
        }

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
    public void RepeatedCyclesKeepOneWeatherListenerAndThreeAuthoredSplashes()
    {
        Fixture fixture = CreateFixture();
        for (int i = 0; i < 8; i++)
        {
            InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Rain);
            InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Storm);
            InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Clear);
        }

        Assert.That(GetSubscriberCount(fixture.Weather), Is.EqualTo(1));
        Assert.That(fixture.Rain.SplashEmitterCount, Is.EqualTo(3));
        Assert.That(fixture.Rain.IsRainRequested, Is.False);
    }

    [Test]
    public void RainAndSplashParticlesUseWorldSimulationWithoutCollision()
    {
        Fixture fixture = CreateFixture();
        ParticleSystem.MainModule rainMain = fixture.Rain.RainLayer.main;
        ParticleSystem.VelocityOverLifetimeModule velocity = fixture.Rain.RainLayer.velocityOverLifetime;
        ParticleSystem.CollisionModule collision = fixture.Rain.RainLayer.collision;

        Assert.That(rainMain.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(velocity.enabled, Is.True);
        Assert.That(velocity.space, Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(collision.enabled, Is.False);

        for (int i = 0; i < fixture.Splashes.Length; i++)
        {
            Assert.That(fixture.Splashes[i].main.simulationSpace,
                Is.EqualTo(ParticleSystemSimulationSpace.World));
        }
    }

    [Test]
    public void CoverageUsesCachedGameplayCameraAndKeepsNarrowDepthBand()
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

        Rect worldRect = CameraInfoCache.WorldRect;
        Assert.That(fixture.Rain.RainLayer.transform.position.x, Is.EqualTo(worldRect.center.x).Within(0.001f));
        Assert.That(fixture.Rain.RainLayer.transform.position.y, Is.EqualTo(worldRect.center.y).Within(0.001f));
        Assert.That(fixture.Rain.RainLayer.shape.scale.x, Is.GreaterThan(worldRect.width));
        Assert.That(fixture.Rain.RainLayer.shape.scale.z, Is.EqualTo(0.25f).Within(0.001f));
    }

    [Test]
    public void RainPresentationDoesNotOwnAudioSources()
    {
        Fixture fixture = CreateFixture();
        Assert.That(fixture.Root.GetComponentsInChildren<AudioSource>(true), Is.Empty);
    }

    [Test]
    public void DisableUnsubscribesAndStopsEmissionIdempotently()
    {
        Fixture fixture = CreateFixture();
        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Rain);
        InvokePrivate(fixture.Rain, "OnDisable");
        InvokePrivate(fixture.Rain, "OnDisable");

        Assert.That(GetSubscriberCount(fixture.Weather), Is.Zero);
        Assert.That(fixture.Rain.IsRainRequested, Is.False);
        Assert.That(fixture.Rain.RainLayer.emission.rateOverTime.constant, Is.EqualTo(0f));
    }

    private Fixture CreateFixture(bool withAudio = false)
    {
        GameObject root = Track(new GameObject("Room Rain Presentation Test"));
        root.SetActive(false);
        RoomWeatherPresentation weather = root.AddComponent<RoomWeatherPresentation>();
        RoomRainPresentation rain = root.AddComponent<RoomRainPresentation>();
        ParticleSystem rainLayer = CreateParticle(root.transform, "WorldRain");
        ParticleSystem[] splashes = new ParticleSystem[3];
        for (int i = 0; i < splashes.Length; i++)
        {
            splashes[i] = CreateParticle(root.transform, $"GroundSplash_{i}");
        }

        SetField(rain, "weatherPresentation", weather);
        SetField(rain, "rainLayer", rainLayer);
        SetField(rain, "splashEmitters", splashes);
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
        return new Fixture(root, weather, rain, splashes, audio, ambienceClip);
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
        public readonly ParticleSystem[] Splashes;
        public readonly AudioManager Audio;
        public readonly AudioClip AmbienceClip;

        public Fixture(
            GameObject root,
            RoomWeatherPresentation weather,
            RoomRainPresentation rain,
            ParticleSystem[] splashes,
            AudioManager audio,
            AudioClip ambienceClip)
        {
            Root = root;
            Weather = weather;
            Rain = rain;
            Splashes = splashes;
            Audio = audio;
            AmbienceClip = ambienceClip;
        }
    }
}
