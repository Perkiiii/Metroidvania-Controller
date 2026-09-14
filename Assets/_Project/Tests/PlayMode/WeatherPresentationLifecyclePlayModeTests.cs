using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Runtime-only lifecycle coverage for the room weather presentation seam. The custom PlayMode
/// assembly cannot reference Assembly-CSharp directly, so production objects are reached by type
/// reflection as in the existing camera PlayMode fixtures.
/// </summary>
public sealed class WeatherPresentationLifecyclePlayModeTests
{
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
    private GameObject room;
    private GameObject audioRoot;
    private Component weather;
    private Component rain;
    private Component storm;
    private Component impactHandler;
    private Component audio;
    private ParticleSystem backRainLayer;
    private ParticleSystem midRainLayer;
    private ParticleSystem frontRainLayer;
    private ParticleSystem splashSystem;
    private SpriteRenderer flashRenderer;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (room != null)
        {
            UnityEngine.Object.Destroy(room);
        }

        if (audioRoot != null)
        {
            UnityEngine.Object.Destroy(audioRoot);
        }

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.Destroy(createdObjects[i]);
            }
        }

        yield return null;
        createdObjects.Clear();
        room = null;
        audioRoot = null;
        weather = null;
        rain = null;
        storm = null;
        impactHandler = null;
        audio = null;
    }

    [UnityTest]
    public IEnumerator RuntimeEnableChangeDisableCycleCleansSubscriptionsEffectsAndAmbience()
    {
        CreateFixture();

        audioRoot.SetActive(true);
        room.SetActive(true);
        yield return null;

        Assert.That(GetSubscriberCount(weather), Is.EqualTo(2),
            "Rain and storm must each subscribe once during the real OnEnable callback.");
        Assert.That((bool)GetProperty(rain, "IsRainRequested"), Is.False);
        Assert.That((bool)GetProperty(storm, "IsStormRequested"), Is.False);

        SetPresentation("Rain");
        yield return null;
        Assert.That((bool)GetProperty(rain, "IsRainRequested"), Is.True);
        Assert.That(backRainLayer.emission.rateOverTime.constant, Is.GreaterThan(0f));
        Assert.That(midRainLayer.emission.rateOverTime.constant, Is.GreaterThan(0f));
        Assert.That(frontRainLayer.emission.rateOverTime.constant, Is.GreaterThan(0f));
        Assert.That((bool)GetProperty(impactHandler, "IsAcceptingImpacts"), Is.True);
        Assert.That(GetProperty(audio, "CurrentAmbienceClip"), Is.SameAs(GetField(rain, "rainAmbienceClip")));
        Assert.That((bool)GetProperty(audio, "IsAmbienceActive"), Is.True);

        SetPresentation("Storm");
        yield return null;
        Assert.That((bool)GetProperty(storm, "IsStormRequested"), Is.True);
        Assert.That((bool)GetProperty(rain, "IsRainRequested"), Is.True,
            "Storm keeps the shared rain layer active while adding storm presentation.");

        SetPresentation("Clear");
        yield return null;
        Assert.That((bool)GetProperty(storm, "IsStormRequested"), Is.False);
        Assert.That((bool)GetProperty(rain, "IsRainRequested"), Is.False);
        Assert.That(backRainLayer.emission.rateOverTime.constant, Is.EqualTo(0f));
        Assert.That(midRainLayer.emission.rateOverTime.constant, Is.EqualTo(0f));
        Assert.That(frontRainLayer.emission.rateOverTime.constant, Is.EqualTo(0f));
        Assert.That(splashSystem.emission.enabled, Is.False);
        Assert.That(splashSystem.particleCount, Is.Zero);
        Assert.That((bool)GetProperty(impactHandler, "IsAcceptingImpacts"), Is.False);

        Assert.That(GetProperty(audio, "CurrentAmbienceClip"), Is.Null);
        Assert.That((bool)GetProperty(audio, "IsAmbienceActive"), Is.False);

        room.SetActive(false);
        yield return null;
        Assert.That(GetSubscriberCount(weather), Is.Zero,
            "Disabling the room must remove both concrete consumer callbacks.");
        Assert.That((bool)GetProperty(storm, "IsStormRequested"), Is.False);
        Assert.That((bool)GetProperty(rain, "IsRainRequested"), Is.False);
        Assert.That(flashRenderer.enabled, Is.False);
        Assert.That((bool)GetProperty(impactHandler, "IsAcceptingImpacts"), Is.False);
        Assert.That(splashSystem.particleCount, Is.Zero);

        room.SetActive(true);
        yield return null;
        Assert.That(GetSubscriberCount(weather), Is.EqualTo(2),
            "Re-enabling a room must restore exactly one listener per consumer.");

        SetPresentation("Rain");
        yield return null;
        Assert.That((bool)GetProperty(rain, "IsRainRequested"), Is.True);
        Assert.That(GetProperty(audio, "CurrentAmbienceClip"), Is.SameAs(GetField(rain, "rainAmbienceClip")));
    }

    private void CreateFixture()
    {
        Type weatherType = RuntimeType("RoomWeatherPresentation");
        Type rainType = RuntimeType("RoomRainPresentation");
        Type impactHandlerType = RuntimeType("RainImpactSplashHandler");
        Type stormType = RuntimeType("RoomStormPresentation");
        Type audioType = RuntimeType("AudioManager");

        audioRoot = Track(new GameObject("Weather Lifecycle Audio"));
        audioRoot.SetActive(false);
        audioRoot.AddComponent<AudioListener>();
        AudioSource sfx = CreateSource(audioRoot.transform, "SFX");
        AudioSource music = CreateSource(audioRoot.transform, "Music");
        AudioSource ambience = CreateSource(audioRoot.transform, "Ambience");
        audio = audioRoot.AddComponent(audioType);
        SetField(audio, "sfxSource", sfx);
        SetField(audio, "musicSource", music);
        SetField(audio, "ambienceSource", ambience);

        room = Track(new GameObject("Weather Lifecycle Room"));
        room.SetActive(false);
        weather = room.AddComponent(weatherType);
        rain = room.AddComponent(rainType);
        storm = room.AddComponent(stormType);

        backRainLayer = CreateParticle(room.transform, "BackRain");
        midRainLayer = CreateParticle(room.transform, "MidRain");
        frontRainLayer = CreateParticle(room.transform, "FrontRain");
        splashSystem = CreateParticle(room.transform, "GroundSplashPool");
        impactHandler = midRainLayer.gameObject.AddComponent(impactHandlerType);

        GameObject flashObject = Track(new GameObject("LightningFlash"));
        flashObject.transform.SetParent(room.transform, false);
        flashRenderer = flashObject.AddComponent<SpriteRenderer>();
        Texture2D texture = Track(new Texture2D(16, 16));
        flashRenderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f),
            new Vector2(0.5f, 0.5f), 16f);

        AudioClip ambienceClip = Track(AudioClip.Create("Lifecycle Rain", 8, 1, 44100, false));
        AudioClip thunderClip = Track(AudioClip.Create("Lifecycle Thunder", 8, 1, 44100, false));
        SetField(impactHandler, "collisionSource", midRainLayer);
        SetField(impactHandler, "splashSystem", splashSystem);
        SetField(impactHandler, "terrainLayers", (LayerMask)(1 << 7));
        SetField(rain, "weatherPresentation", weather);
        SetField(rain, "backRainLayer", backRainLayer);
        SetField(rain, "midRainLayer", midRainLayer);
        SetField(rain, "frontRainLayer", frontRainLayer);
        SetField(rain, "splashSystem", splashSystem);
        SetField(rain, "impactSplashHandler", impactHandler);
        SetField(rain, "terrainCollisionLayers", (LayerMask)(1 << 7));
        SetField(rain, "rainAmbienceClip", ambienceClip);
        SetField(rain, "ambienceFadeSeconds", 0f);

        SetField(storm, "weatherPresentation", weather);
        SetField(storm, "flashRenderer", flashRenderer);
        SetField(storm, "thunderClip", thunderClip);
        SetField(storm, "initialStrikeDelay", 100f);
    }

    private void SetPresentation(string mode)
    {
        Type modeType = RuntimeType("RoomWeatherPresentationMode");
        Invoke(weather, "SetPresentation", Enum.Parse(modeType, mode));
    }

    private ParticleSystem CreateParticle(Transform parent, string name)
    {
        GameObject particleObject = Track(new GameObject(name));
        particleObject.transform.SetParent(parent, false);
        return particleObject.AddComponent<ParticleSystem>();
    }

    private AudioSource CreateSource(Transform parent, string name)
    {
        GameObject sourceObject = Track(new GameObject(name));
        sourceObject.transform.SetParent(parent, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return source;
    }

    private int GetSubscriberCount(Component instance)
    {
        FieldInfo field = instance.GetType().GetField(
            "PresentationChanged",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Delegate callback = field.GetValue(instance) as Delegate;
        return callback == null ? 0 : callback.GetInvocationList().Length;
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        createdObjects.Add(value);
        return value;
    }

    private static Type RuntimeType(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, "Runtime type was not found: " + name);
        return type;
    }

    private static object GetProperty(object instance, string property)
    {
        PropertyInfo info = instance.GetType().GetProperty(
            property,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, "Property was not found: " + property);
        return info.GetValue(instance);
    }

    private static object GetField(object instance, string fieldName)
    {
        FieldInfo info = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, "Field was not found: " + fieldName);
        return info.GetValue(instance);
    }

    private static void SetField(object instance, string fieldName, object value)
    {
        FieldInfo info = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, "Field was not found: " + fieldName);
        info.SetValue(instance, value);
    }

    private static object Invoke(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Method was not found: " + methodName);
        return method.Invoke(instance, arguments);
    }
}
