using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class RoomStormPresentationTests
{
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

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
    public void StormStartsAReadableScheduleAndUsesMultipleFlashPulsesBeforeThunder()
    {
        Fixture fixture = CreateFixture(withAudio: true);
        SetField(fixture.Storm, "initialStrikeDelay", 0f);
        SetField(fixture.Storm, "flashPulseCount", 3);
        SetField(fixture.Storm, "flashPulseDuration", 0.05f);
        SetField(fixture.Storm, "flashPulseGap", 0.03f);
        SetField(fixture.Storm, "thunderDelaySeconds", 0.25f);

        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Storm);
        Assert.That(fixture.Storm.CurrentState,
            Is.EqualTo(RoomStormPresentationState.WaitingForStrike));
        Assert.That(fixture.Storm.IsThunderPending, Is.False);
        Assert.That(fixture.Storm.FlashRenderer.enabled, Is.False);

        Tick(fixture.Storm, 0f);
        Assert.That(fixture.Storm.CurrentState,
            Is.EqualTo(RoomStormPresentationState.Flashing));
        Assert.That(fixture.Storm.FlashAmount, Is.GreaterThan(0f));

        Tick(fixture.Storm, 0.2f);
        Assert.That(fixture.Storm.CurrentState,
            Is.EqualTo(RoomStormPresentationState.Flashing));
        Assert.That(fixture.Storm.FlashAmount, Is.GreaterThan(0f));
        Assert.That(fixture.Storm.StrikeCount, Is.Zero);

        Tick(fixture.Storm, 0.2f);
        Assert.That(fixture.Storm.CurrentState,
            Is.EqualTo(RoomStormPresentationState.WaitingForThunder));
        Assert.That(fixture.Storm.IsThunderPending, Is.True);
        Assert.That(fixture.Storm.FlashAmount, Is.EqualTo(0f));
        Assert.That(fixture.Storm.StrikeCount, Is.EqualTo(1));
        Assert.That(fixture.Storm.ThunderPlayCount, Is.Zero);

        Tick(fixture.Storm, 0.24f);
        Assert.That(fixture.Storm.ThunderPlayCount, Is.Zero);
        Assert.That(fixture.Storm.IsThunderPending, Is.True);

        Tick(fixture.Storm, 0.02f);
        Assert.That(fixture.Storm.ThunderPlayCount, Is.EqualTo(1));
        Assert.That(fixture.Storm.CurrentState,
            Is.EqualTo(RoomStormPresentationState.WaitingForStrike));
    }

    [Test]
    public void RainAndClearCancelPendingThunderAndHideTheFlash()
    {
        Fixture fixture = CreateFixture();
        SetField(fixture.Storm, "initialStrikeDelay", 0f);
        SetField(fixture.Storm, "flashPulseCount", 1);
        SetField(fixture.Storm, "flashPulseDuration", 0.01f);
        SetField(fixture.Storm, "thunderDelaySeconds", 1f);

        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Storm);
        Tick(fixture.Storm, 0f);
        Tick(fixture.Storm, 0.02f);
        Assert.That(fixture.Storm.IsThunderPending, Is.True);
        Assert.That(fixture.Storm.FlashRenderer.enabled, Is.False);

        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Rain);
        AssertStormCancelled(fixture.Storm);
        Tick(fixture.Storm, 2f);
        Assert.That(fixture.Storm.ThunderPlayCount, Is.Zero);

        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Storm);
        Tick(fixture.Storm, 0f);
        Tick(fixture.Storm, 0.02f);
        Assert.That(fixture.Storm.IsThunderPending, Is.True);
        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Clear);
        AssertStormCancelled(fixture.Storm);
        Tick(fixture.Storm, 2f);
        Assert.That(fixture.Storm.ThunderPlayCount, Is.Zero);
    }

    [Test]
    public void DisableAndDestroyUnsubscribeAndCancelWaitingPhasesIdempotently()
    {
        Fixture fixture = CreateFixture();
        SetField(fixture.Storm, "initialStrikeDelay", 10f);
        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Storm);
        Assert.That(GetSubscriberCount(fixture.Weather), Is.EqualTo(1));

        InvokePrivate(fixture.Storm, "OnDisable");
        InvokePrivate(fixture.Storm, "OnDisable");
        AssertStormCancelled(fixture.Storm);
        Assert.That(GetSubscriberCount(fixture.Weather), Is.Zero);

        InvokePrivate(fixture.Storm, "OnDestroy");
        InvokePrivate(fixture.Storm, "OnDestroy");
        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Storm);
        Tick(fixture.Storm, 20f);
        Assert.That(fixture.Storm.ThunderPlayCount, Is.Zero);
        Assert.That(fixture.Storm.CurrentState,
            Is.EqualTo(RoomStormPresentationState.Inactive));
    }

    [Test]
    public void RepeatedStormCyclesKeepOneListenerAndOnePendingSequence()
    {
        Fixture fixture = CreateFixture(withAudio: true);
        SetField(fixture.Storm, "initialStrikeDelay", 0f);
        SetField(fixture.Storm, "strikeIntervalMinSeconds", 0.1f);
        SetField(fixture.Storm, "strikeIntervalMaxSeconds", 0.1f);
        SetField(fixture.Storm, "flashPulseCount", 1);
        SetField(fixture.Storm, "flashPulseDuration", 0.01f);
        SetField(fixture.Storm, "thunderDelaySeconds", 0.01f);

        for (int i = 0; i < 5; i++)
        {
            InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Storm);
            TickUntilThunder(fixture.Storm);
            Assert.That(fixture.Storm.IsThunderPending, Is.True);
            Tick(fixture.Storm, 0.02f);
            Assert.That(fixture.Storm.IsThunderPending, Is.False);
            InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Rain);
            AssertStormCancelled(fixture.Storm);
            InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Storm);
        }

        Assert.That(GetSubscriberCount(fixture.Weather), Is.EqualTo(1));
        Assert.That(fixture.Storm.IsThunderPending, Is.False);
        Assert.That(fixture.Storm.CurrentState,
            Is.EqualTo(RoomStormPresentationState.WaitingForStrike));
        Assert.That(fixture.Storm.ThunderPlayCount, Is.EqualTo(5));
    }

    [Test]
    public void FlashFollowsCachedGameplayViewAtWeatherDepth()
    {
        Fixture fixture = CreateFixture();
        GameObject cameraObject = Track(new GameObject("Storm Coverage Camera"));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(4f, 2f, -38.1f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 24f;
        camera.aspect = 16f / 9f;
        CameraInfoCache.UpdateCache(camera, true);

        SetField(fixture.Storm, "cameraPadding", 0.5f);
        InvokePrivate(fixture.Storm, "LateUpdate");

        Rect worldRect = CameraInfoCache.WorldRect;
        Assert.That(fixture.Storm.FlashRenderer.transform.position.x,
            Is.EqualTo(worldRect.center.x).Within(0.001f));
        Assert.That(fixture.Storm.FlashRenderer.transform.position.y,
            Is.EqualTo(worldRect.center.y).Within(0.001f));
        Assert.That(fixture.Storm.FlashRenderer.transform.position.z,
            Is.EqualTo(camera.transform.position.z + 37.8f).Within(0.001f));
        Assert.That(fixture.Storm.FlashRenderer.bounds.size.x,
            Is.GreaterThan(worldRect.width));
        Assert.That(fixture.Storm.FlashRenderer.bounds.size.y,
            Is.GreaterThan(worldRect.height));
    }

    [Test]
    public void ThunderUsesAudioManagerPooledSfxPathAndStormOwnsNoAudioSource()
    {
        Fixture fixture = CreateFixture(withAudio: true);
        SetField(fixture.Storm, "initialStrikeDelay", 0f);
        SetField(fixture.Storm, "flashPulseCount", 1);
        SetField(fixture.Storm, "flashPulseDuration", 0.01f);
        SetField(fixture.Storm, "thunderDelaySeconds", 0f);

        InvokePrivate(fixture.Weather, "SetPresentation", RoomWeatherPresentationMode.Storm);
        Tick(fixture.Storm, 0f);
        Tick(fixture.Storm, 0.02f);
        Tick(fixture.Storm, 0f);

        Assert.That(fixture.Storm.ThunderPlayCount, Is.EqualTo(1));
        Assert.That(fixture.Root.GetComponentsInChildren<AudioSource>(true), Is.Empty);

        AudioSource[] sources = fixture.Audio.GetComponentsInChildren<AudioSource>(true);
        Assert.That(sources.Length, Is.EqualTo(4));
        AudioSource pooled = Array.Find(sources, source => source.clip == fixture.ThunderClip);
        Assert.That(pooled, Is.Not.Null);
        Assert.That(pooled.clip, Is.SameAs(fixture.ThunderClip));
        Assert.That(pooled.pitch, Is.InRange(0.96f, 1.04f));
        Assert.That(pooled.volume, Is.EqualTo(0.55f).Within(0.001f));
    }

    private void TickUntilThunder(RoomStormPresentation storm)
    {
        for (int i = 0; i < 12 && !storm.IsThunderPending; i++)
        {
            Tick(storm, 0.1f);
        }

        Assert.That(storm.IsThunderPending, Is.True);
    }

    private static void Tick(RoomStormPresentation storm, float deltaTime)
    {
        InvokePrivate(storm, "TickStorm", deltaTime);
    }

    private static void AssertStormCancelled(RoomStormPresentation storm)
    {
        Assert.That(storm.IsStormRequested, Is.False);
        Assert.That(storm.IsThunderPending, Is.False);
        Assert.That(storm.CurrentState,
            Is.EqualTo(RoomStormPresentationState.Inactive));
        Assert.That(storm.FlashAmount, Is.EqualTo(0f));
        Assert.That(storm.FlashRenderer.enabled, Is.False);
    }

    private Fixture CreateFixture(bool withAudio = false)
    {
        GameObject root = Track(new GameObject("Room Storm Presentation Test"));
        root.SetActive(false);
        RoomWeatherPresentation weather = root.AddComponent<RoomWeatherPresentation>();
        RoomStormPresentation storm = root.AddComponent<RoomStormPresentation>();

        GameObject flashObject = Track(new GameObject("LightningFlash"));
        flashObject.transform.SetParent(root.transform, false);
        SpriteRenderer flashRenderer = flashObject.AddComponent<SpriteRenderer>();
        flashRenderer.sprite = CreateSprite();
        flashRenderer.enabled = true;

        AudioManager audio = null;
        AudioClip thunderClip = Track(AudioClip.Create("Thunder Test", 16, 1, 44100, false));
        if (withAudio)
        {
            audio = CreateAudioManager();
        }

        SetField(storm, "weatherPresentation", weather);
        SetField(storm, "flashRenderer", flashRenderer);
        SetField(storm, "thunderClip", thunderClip);
        SetField(storm, "thunderVolume", 0.55f);
        SetField(storm, "thunderPitchMin", 0.96f);
        SetField(storm, "thunderPitchMax", 1.04f);

        InvokePrivate(storm, "Awake");
        InvokePrivate(weather, "OnEnable");
        InvokePrivate(storm, "OnEnable");
        return new Fixture(root, weather, storm, flashRenderer, audio, thunderClip);
    }

    private AudioManager CreateAudioManager()
    {
        if (AudioManager.Instance != null)
        {
            InvokePrivate(AudioManager.Instance, "OnDestroy");
        }

        GameObject root = Track(new GameObject("Storm Audio Manager Test"));
        AudioSource sfx = CreateSource(root.transform, "SFX");
        AudioSource music = CreateSource(root.transform, "Music");
        AudioSource ambience = CreateSource(root.transform, "Ambience");
        AudioManager manager = root.AddComponent<AudioManager>();
        SetField(manager, "sfxSource", sfx);
        SetField(manager, "musicSource", music);
        SetField(manager, "ambienceSource", ambience);
        InvokePrivate(manager, "Awake");
        return manager;
    }

    private AudioSource CreateSource(Transform parent, string name)
    {
        GameObject sourceObject = Track(new GameObject(name));
        sourceObject.transform.SetParent(parent, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return source;
    }

    private Sprite CreateSprite()
    {
        Texture2D texture = Track(new Texture2D(64, 64));
        return Sprite.Create(texture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f);
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
        public readonly RoomStormPresentation Storm;
        public readonly SpriteRenderer FlashRenderer;
        public readonly AudioManager Audio;
        public readonly AudioClip ThunderClip;

        public Fixture(
            GameObject root,
            RoomWeatherPresentation weather,
            RoomStormPresentation storm,
            SpriteRenderer flashRenderer,
            AudioManager audio,
            AudioClip thunderClip)
        {
            Root = root;
            Weather = weather;
            Storm = storm;
            FlashRenderer = flashRenderer;
            Audio = audio;
            ThunderClip = thunderClip;
        }
    }
}
