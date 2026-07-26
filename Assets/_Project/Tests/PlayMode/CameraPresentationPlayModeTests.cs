using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

// Camera Phase 3 behaviour that only exists at runtime: zoom smoothing across real frames, and
// Timeline request ownership under a genuinely playing PlayableDirector.
//
// Types are resolved reflectively because this assembly cannot reference the predefined
// Assembly-CSharp, matching CameraLifecyclePlayModeTests.
public sealed class CameraPresentationPlayModeTests
{
    private Type controllerType;
    private Type targetType;
    private Type camerasType;
    private Type settingsType;
    private Type modeType;
    private Type lifetimeType;
    private Type receiverType;
    private Type trackType;
    private Type clipType;

    private GameObject hero;
    private GameObject camerasRoot;
    private Component controller;
    private Component target;
    private Component cameras;
    private Camera cam;

    private readonly List<GameObject> temporaries = new List<GameObject>();
    private TimelineAsset timeline;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        controllerType = RuntimeType("CameraController");
        targetType = RuntimeType("CameraTarget");
        camerasType = RuntimeType("GameCameras");
        settingsType = RuntimeType("CameraPresentationSettings");
        modeType = RuntimeType("CameraPresentationMode");
        lifetimeType = RuntimeType("CameraRequestLifetime");
        receiverType = RuntimeType("CameraPresentationReceiver");
        trackType = RuntimeType("CameraPresentationTrack");
        clipType = RuntimeType("CameraPresentationClip");

        hero = new GameObject("PlayMode Presentation Hero") { tag = "Player" };
        hero.AddComponent<BoxCollider2D>().size = new Vector2(1f, 2f);
        Rigidbody2D body = hero.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;

        camerasRoot = new GameObject("PlayMode Game Cameras");
        cameras = camerasRoot.AddComponent(camerasType);

        GameObject targetObject = new GameObject("Camera Target");
        targetObject.transform.SetParent(camerasRoot.transform);
        target = targetObject.AddComponent(targetType);

        GameObject controllerObject = new GameObject("Camera Controller");
        controllerObject.transform.SetParent(camerasRoot.transform);
        cam = controllerObject.AddComponent<Camera>();
        controller = controllerObject.AddComponent(controllerType);
        SetField(controller, "cameraTarget", target);
        SetField(cameras, "cameraController", controller);
        SetField(cameras, "cameraTarget", target);

        Invoke(target, "SceneInit");
        Invoke(controller, "SceneInit");
        Invoke(cameras, "OnSceneInit", SceneManager.GetActiveScene());

        // Consume the scene-start snap window so presentation influences framing immediately.
        SetField(controller, "startTimer", 0f);
        Physics2D.SyncTransforms();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int i = 0; i < temporaries.Count; i++)
        {
            if (temporaries[i] != null)
            {
                UnityEngine.Object.Destroy(temporaries[i]);
            }
        }

        temporaries.Clear();

        if (timeline != null)
        {
            UnityEngine.Object.Destroy(timeline);
            timeline = null;
        }

        if (camerasRoot != null) UnityEngine.Object.Destroy(camerasRoot);
        if (hero != null) UnityEngine.Object.Destroy(hero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ZoomSmoothsAcrossFramesAndSettlesOnTheAuthoredValue()
    {
        ClearSceneStartSnapWindow();
        Transform focus = CreateTarget("Focus", new Vector3(4f, 0f, 0f));
        object settings = BuildSettings("FocusTarget", autoZoom: false, authoredZoom: 1.3f, minZoom: 0.5f, maxZoom: 2f);
        object handle = AcquirePresentation(settings, new[] { focus }, priority: 0);

        float startZoom = CurrentZoom;
        Assert.That(startZoom, Is.EqualTo(1f).Within(0.01f));

        yield return null;
        float afterOneFrame = CurrentZoom;
        Assert.That(afterOneFrame, Is.GreaterThan(startZoom), "Zoom must move toward the request.");
        Assert.That(afterOneFrame, Is.LessThan(1.3f), "Zoom must smooth rather than snap.");

        yield return WaitForZoom(1.3f, 0.02f, 6f);

        Assert.That(CurrentZoom, Is.EqualTo(1.3f).Within(0.02f));

        // Zoom is applied purely as field of view, derived from the authored base FOV.
        Assert.That(cam.fieldOfView, Is.EqualTo(FieldOfViewForZoom(CurrentZoom)).Within(0.05f));

        Release(handle);
        yield return WaitForZoom(1f, 0.02f, 6f);

        Assert.That(CurrentZoom, Is.EqualTo(1f).Within(0.02f), "Releasing must return zoom to the authored base.");
        Assert.That(cam.fieldOfView, Is.EqualTo(FieldOfViewForZoom(CurrentZoom)).Within(0.05f));
    }

    [UnityTest]
    public IEnumerator ZoomDoesNotMoveWhileAHardFreezeIsHeld()
    {
        ClearSceneStartSnapWindow();
        Transform focus = CreateTarget("Focus", new Vector3(4f, 0f, 0f));
        object freeze = Invoke(cameras, "AcquireFreeze",
            Enum.Parse(RuntimeType("CameraFreezeKind"), "Hard"),
            -1f,
            (object)this,
            Enum.Parse(lifetimeType, "Scene"));

        object settings = BuildSettings("FocusTarget", autoZoom: false, authoredZoom: 1.4f, minZoom: 0.5f, maxZoom: 2f);
        AcquirePresentation(settings, new[] { focus }, 0);

        float frozenZoom = CurrentZoom;
        Vector3 frozenPosition = ((Component)controller).transform.position;
        yield return WaitFrames(10);

        Assert.That(CurrentZoom, Is.EqualTo(frozenZoom).Within(0.0001f));
        Assert.That(((Component)controller).transform.position, Is.EqualTo(frozenPosition));

        Invoke(freeze, "Release");
        yield return WaitFrames(10);

        Assert.That(CurrentZoom, Is.GreaterThan(frozenZoom), "Freeze release must resume the presentation.");
    }

    [UnityTest]
    public IEnumerator PlayingDirectorAcquiresAndStoppingReleasesTheTimelineRequest()
    {
        Transform focus = CreateTarget("Focus", new Vector3(10f, 2f, 0f));

        GameObject directorObject = new GameObject("Camera Presentation");
        temporaries.Add(directorObject);
        PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
        Component receiver = directorObject.AddComponent(receiverType);
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;

        timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        TrackAsset track = (TrackAsset)typeof(TimelineAsset)
            .GetMethod("CreateTrack", new[] { typeof(Type), typeof(TrackAsset), typeof(string) })
            .Invoke(timeline, new object[] { trackType, null, "Camera Presentation" });

        TimelineClip clip = CreateClip(track);
        clip.start = 0d;
        clip.duration = 3d;

        ScriptableObject clipAsset = (ScriptableObject)clip.asset;
        SetField(clipAsset, "settings", BuildSettings("FocusTarget", false, 1f, 0.5f, 2f));
        SetField(clipAsset, "priority", 5);

        Array exposed = Array.CreateInstance(typeof(ExposedReference<Transform>), 1);
        ExposedReference<Transform> reference = new ExposedReference<Transform>
        {
            exposedName = new PropertyName("PlayModeCameraPresentationTarget")
        };
        exposed.SetValue(reference, 0);
        SetField(clipAsset, "targets", exposed);
        director.SetReferenceValue(reference.exposedName, focus);

        director.playableAsset = timeline;
        director.SetGenericBinding(track, receiver);

        director.Play();
        yield return null;
        yield return null;

        Assert.That(ActivePresentationCount, Is.EqualTo(1), "A playing director must own exactly one request.");
        Assert.That((int)GetProperty(receiver, "ActiveHandleCount"), Is.EqualTo(1));
        Assert.That((bool)GetProperty(controller, "HasPresentationRequest"), Is.True);
        Assert.That((int)GetProperty(controller, "PresentationPriority"), Is.EqualTo(5));

        // Repeated frames must not leak duplicates.
        yield return WaitFrames(20);
        Assert.That(ActivePresentationCount, Is.EqualTo(1));

        director.Stop();
        yield return null;
        yield return null;

        Assert.That(ActivePresentationCount, Is.Zero, "Stopping the director must release its request.");
        Assert.That((int)GetProperty(receiver, "ActiveHandleCount"), Is.Zero);
        Assert.That((bool)GetProperty(controller, "HasPresentationRequest"), Is.False);
    }

    [UnityTest]
    public IEnumerator DestroyingTheReceiverReleasesItsTimelineRequest()
    {
        Transform focus = CreateTarget("Focus", new Vector3(10f, 2f, 0f));

        GameObject directorObject = new GameObject("Camera Presentation");
        PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
        Component receiver = directorObject.AddComponent(receiverType);
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;

        timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        TrackAsset track = (TrackAsset)typeof(TimelineAsset)
            .GetMethod("CreateTrack", new[] { typeof(Type), typeof(TrackAsset), typeof(string) })
            .Invoke(timeline, new object[] { trackType, null, "Camera Presentation" });

        TimelineClip clip = CreateClip(track);
        clip.start = 0d;
        clip.duration = 5d;

        ScriptableObject clipAsset = (ScriptableObject)clip.asset;
        SetField(clipAsset, "settings", BuildSettings("FocusTarget", false, 1f, 0.5f, 2f));

        Array exposed = Array.CreateInstance(typeof(ExposedReference<Transform>), 1);
        ExposedReference<Transform> reference = new ExposedReference<Transform>
        {
            exposedName = new PropertyName("PlayModeCameraPresentationTarget2")
        };
        exposed.SetValue(reference, 0);
        SetField(clipAsset, "targets", exposed);
        director.SetReferenceValue(reference.exposedName, focus);

        director.playableAsset = timeline;
        director.SetGenericBinding(track, receiver);
        director.Play();
        yield return null;
        yield return null;
        Assert.That(ActivePresentationCount, Is.EqualTo(1));

        UnityEngine.Object.Destroy(directorObject);
        yield return null;
        yield return null;

        Assert.That(ActivePresentationCount, Is.Zero, "Destroying the adapter must release its request.");
    }

    // --------------------------------------------------------------------------------- helpers

    // GameCameras.Start re-runs scene init on the first frame, which re-arms the scene-start snap
    // timer. Presentation deliberately does not influence framing inside that window, so tests
    // that assert live framing clear it in the test body rather than in SetUp.
    private void ClearSceneStartSnapWindow()
    {
        SetField(controller, "startTimer", 0f);
    }

    private const float BaseFieldOfView = 24f;

    private static float FieldOfViewForZoom(float zoom)
    {
        return 2f * Mathf.Atan(Mathf.Tan(BaseFieldOfView * 0.5f * Mathf.Deg2Rad) * zoom) * Mathf.Rad2Deg;
    }

    private float CurrentZoom => (float)GetProperty(controller, "CurrentZoom");

    private int ActivePresentationCount => (int)GetProperty(cameras, "ActivePresentationCount");

    // Zoom smoothing is time-based, so settling is awaited by value with a timeout rather than by
    // a fixed frame count that would depend on the editor's frame rate.
    private IEnumerator WaitForZoom(float expected, float tolerance, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Mathf.Abs(CurrentZoom - expected) > tolerance && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
    }

    private static IEnumerator WaitFrames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return null;
        }
    }

    private Transform CreateTarget(string name, Vector3 position)
    {
        GameObject go = new GameObject(name);
        go.transform.position = position;
        temporaries.Add(go);
        return go.transform;
    }

    private object BuildSettings(string mode, bool autoZoom, float authoredZoom, float minZoom, float maxZoom)
    {
        object settings = settingsType
            .GetMethod("Default", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new[] { Enum.Parse(modeType, mode) });

        SetField(settings, "autoZoom", autoZoom);
        SetField(settings, "authoredZoom", authoredZoom);
        SetField(settings, "overrideZoomLimits", true);
        SetField(settings, "minZoom", minZoom);
        SetField(settings, "maxZoom", maxZoom);
        return settings;
    }

    private object AcquirePresentation(object settings, Transform[] targets, int priority)
    {
        MethodInfo method = camerasType.GetMethod("AcquirePresentation", BindingFlags.Public | BindingFlags.Instance);
        return method.Invoke(cameras, new[]
        {
            settings,
            targets,
            (object)this,
            Enum.Parse(lifetimeType, "Scene"),
            priority,
            -1f
        });
    }

    // TrackAsset only exposes the generic CreateClip<T>(), so the clip type is bound reflectively.
    private TimelineClip CreateClip(TrackAsset track)
    {
        foreach (MethodInfo candidate in typeof(TrackAsset).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (candidate.Name == "CreateClip"
                && candidate.IsGenericMethodDefinition
                && candidate.GetParameters().Length == 0)
            {
                return (TimelineClip)candidate.MakeGenericMethod(clipType).Invoke(track, null);
            }
        }

        Assert.Fail("TrackAsset.CreateClip<T>() was not found.");
        return null;
    }

    private static void Release(object handle)
    {
        handle.GetType().GetMethod("Release").Invoke(handle, null);
    }

    private static Type RuntimeType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name, false);
            if (type != null)
            {
                return type;
            }
        }

        Assert.Fail($"Runtime type '{name}' was not found.");
        return null;
    }

    private static object GetProperty(object target, string property)
    {
        return target.GetType()
            .GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .GetValue(target);
    }

    private static void SetField(object target, string field, object value)
    {
        // Structs are boxed here, so the caller keeps using the boxed instance it passed in.
        target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SetValue(target, value);
    }

    private static object Invoke(object target, string method, params object[] arguments)
    {
        foreach (MethodInfo candidate in target.GetType().GetMethods(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (candidate.Name == method && candidate.GetParameters().Length == arguments.Length)
            {
                return candidate.Invoke(target, arguments);
            }
        }

        Assert.Fail($"Method '{method}' was not found on {target.GetType().Name}.");
        return null;
    }
}
