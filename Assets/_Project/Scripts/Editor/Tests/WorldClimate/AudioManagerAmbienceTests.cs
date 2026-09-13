using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class AudioManagerAmbienceTests
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
    }

    [Test]
    public void SameClipRequestsAreIdempotentAndUseOneManagedChannel()
    {
        Fixture fixture = CreateFixture();
        fixture.Manager.StartAmbience(fixture.Root, fixture.Clip, 0.3f, 0f);
        fixture.Manager.StartAmbience(fixture.Root, fixture.Clip, 0.6f, 0f);

        Assert.That(fixture.Manager.CurrentAmbienceClip, Is.SameAs(fixture.Clip));
        Assert.That(fixture.Manager.AmbienceTargetVolume, Is.EqualTo(0.6f));
        Assert.That(fixture.Ambience.volume, Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(fixture.Root.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(3));
    }

    [Test]
    public void StopAmbienceFadesThenClearsTheChannel()
    {
        Fixture fixture = CreateFixture();
        fixture.Manager.StartAmbience(fixture.Root, fixture.Clip, 1f, 0f);
        fixture.Manager.StopAmbience(fixture.Root, 0.5f);

        InvokePrivate(fixture.Manager, "UpdateAmbienceFade", 0.25f);
        Assert.That(fixture.Ambience.volume, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(fixture.Manager.CurrentAmbienceClip, Is.SameAs(fixture.Clip));

        InvokePrivate(fixture.Manager, "UpdateAmbienceFade", 0.25f);
        Assert.That(fixture.Manager.CurrentAmbienceClip, Is.Null);
        Assert.That(fixture.Manager.IsAmbienceActive, Is.False);
    }

    [Test]
    public void ReplacingClipStopsOldClipBeforeStartingNewOne()
    {
        Fixture fixture = CreateFixture();
        AudioClip replacement = Track(AudioClip.Create("Replacement Ambience", 16, 1, 44100, false));
        fixture.Manager.StartAmbience(fixture.Root, fixture.Clip, 0.4f, 0f);
        fixture.Manager.StartAmbience(fixture.Root, replacement, 0.5f, 0f);

        Assert.That(fixture.Manager.CurrentAmbienceClip, Is.SameAs(replacement));
        Assert.That(fixture.Ambience.volume, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(fixture.Ambience.loop, Is.True);
    }

    [Test]
    public void StaleOwnerCannotStopOrUpdateAReplacementUsingTheSameClip()
    {
        Fixture fixture = CreateFixture();
        GameObject ownerA = Track(new GameObject("Rain Owner A"));
        GameObject ownerB = Track(new GameObject("Rain Owner B"));

        fixture.Manager.StartAmbience(ownerA, fixture.Clip, 0.4f, 0f);
        fixture.Manager.StartAmbience(ownerB, fixture.Clip, 0.6f, 0f);

        fixture.Manager.StopAmbience(ownerA, 0f);
        fixture.Manager.UpdateAmbience(ownerA, 0f, 0f);

        Assert.That(fixture.Manager.CurrentAmbienceClip, Is.SameAs(fixture.Clip));
        Assert.That(fixture.Manager.AmbienceTargetVolume, Is.EqualTo(0.6f));
        Assert.That(fixture.Ambience.volume, Is.EqualTo(0.6f).Within(0.001f));

        fixture.Manager.StopAmbience(ownerB, 0f);
        Assert.That(fixture.Manager.CurrentAmbienceClip, Is.Null);
        Assert.That(fixture.Manager.IsAmbienceActive, Is.False);
    }

    private Fixture CreateFixture()
    {
        if (AudioManager.Instance != null)
        {
            InvokePrivate(AudioManager.Instance, "OnDestroy");
        }

        GameObject root = Track(new GameObject("Audio Manager Ambience Test"));
        AudioSource sfx = CreateSource(root.transform, "SFX");
        AudioSource music = CreateSource(root.transform, "Music");
        AudioSource ambience = CreateSource(root.transform, "Ambience");
        AudioManager manager = root.AddComponent<AudioManager>();
        SetField(manager, "sfxSource", sfx);
        SetField(manager, "musicSource", music);
        SetField(manager, "ambienceSource", ambience);
        AudioClip clip = Track(AudioClip.Create("Rain Ambience", 16, 1, 44100, false));
        InvokePrivate(manager, "Awake");
        return new Fixture(root, manager, ambience, clip);
    }

    private AudioSource CreateSource(Transform parent, string name)
    {
        GameObject sourceObject = Track(new GameObject(name));
        sourceObject.transform.SetParent(parent, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return source;
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
        public readonly AudioManager Manager;
        public readonly AudioSource Ambience;
        public readonly AudioClip Clip;

        public Fixture(GameObject root, AudioManager manager, AudioSource ambience, AudioClip clip)
        {
            Root = root;
            Manager = manager;
            Ambience = ambience;
            Clip = clip;
        }
    }
}
