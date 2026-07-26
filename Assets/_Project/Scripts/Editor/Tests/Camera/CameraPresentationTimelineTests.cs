using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Camera Phase 3 Timeline adapter: one handle per track for the life of the graph, no duplicate
// requests under repeated evaluation, and deterministic cleanup on stop/destroy/disable.
public sealed class CameraPresentationTimelineTests
{
    private GameObject hero;
    private GameObject targetObject;
    private GameObject controllerObject;
    private GameObject camerasObject;
    private GameObject directorObject;
    private CameraTarget target;
    private CameraController controller;
    private GameCameras cameras;
    private PlayableDirector director;
    private CameraPresentationReceiver receiver;
    private TimelineAsset timeline;
    private readonly List<GameObject> temporaries = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        hero = new GameObject("Camera Test Hero") { tag = "Player" };
        hero.AddComponent<BoxCollider2D>();

        targetObject = new GameObject("Camera Target");
        target = targetObject.AddComponent<CameraTarget>();

        controllerObject = new GameObject("Camera Controller");
        controllerObject.AddComponent<Camera>();
        controller = controllerObject.AddComponent<CameraController>();
        SetField(controller, "cameraTarget", target);
        Invoke(controller, "Awake");
        target.SceneInit();
        controller.SceneInit();
        SetField(target, "currentVerticalOffset", 0f);
        SetField(controller, "startTimer", 0f);

        camerasObject = new GameObject("Game Cameras");
        cameras = camerasObject.AddComponent<GameCameras>();
        SetField(cameras, "cameraTarget", target);
        SetField(cameras, "cameraController", controller);
        EnsureCamerasInstance();

        directorObject = new GameObject("Camera Presentation");
        director = directorObject.AddComponent<PlayableDirector>();
        receiver = directorObject.AddComponent<CameraPresentationReceiver>();
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;
    }

    [TearDown]
    public void TearDown()
    {
        if (director != null && director.playableGraph.IsValid())
        {
            director.Stop();
        }

        if (timeline != null)
        {
            Object.DestroyImmediate(timeline, true);
            timeline = null;
        }

        for (int i = 0; i < temporaries.Count; i++)
        {
            if (temporaries[i] != null)
            {
                Object.DestroyImmediate(temporaries[i]);
            }
        }

        temporaries.Clear();

        Object.DestroyImmediate(directorObject);

        if (GameCameras.Instance != null)
        {
            Object.DestroyImmediate(GameCameras.Instance.gameObject);
        }

        if (camerasObject != null)
        {
            Object.DestroyImmediate(camerasObject);
        }

        Object.DestroyImmediate(controllerObject);
        Object.DestroyImmediate(targetObject);
        Object.DestroyImmediate(hero);
    }

    [Test]
    public void ClipActivationCreatesTheRequestItOwns()
    {
        Transform focus = CreateTarget("Focus", new Vector3(15f, 2f, 0f));
        BuildTimeline(focus, priority: 12);

        Evaluate(1.0d);

        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));
        Assert.That(receiver.ActiveHandleCount, Is.EqualTo(1));
        Assert.That(controller.HasPresentationRequest, Is.True);
        Assert.That(controller.PresentationPriority, Is.EqualTo(12));
        Assert.That(controller.PresentationSettings.mode, Is.EqualTo(CameraPresentationMode.FocusTarget));
    }

    [Test]
    public void RepeatedEvaluationDoesNotLeakDuplicateRequests()
    {
        BuildTimeline(CreateTarget("Focus", Vector3.zero), 0);

        for (int i = 0; i < 40; i++)
        {
            Evaluate(0.2d + i * 0.02d);
        }

        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));
        Assert.That(receiver.ActiveHandleCount, Is.EqualTo(1));
    }

    [Test]
    public void ScrubbingBackAndForwardDoesNotLeakRequests()
    {
        BuildTimeline(CreateTarget("Focus", Vector3.zero), 0);

        double[] times = { 0.5d, 1.5d, 0.1d, 1.9d, 0.75d, 1.2d };
        for (int i = 0; i < times.Length; i++)
        {
            Evaluate(times[i]);
            Assert.That(cameras.ActivePresentationCount, Is.LessThanOrEqualTo(1));
        }

        Assert.That(receiver.ActiveHandleCount, Is.EqualTo(1));
    }

    [Test]
    public void EvaluatingPastTheClipReleasesTheRequest()
    {
        BuildTimeline(CreateTarget("Focus", Vector3.zero), 0);
        Evaluate(1.0d);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));

        // Beyond the clip every input weight is zero, which is the mixer's release condition.
        Evaluate(4.0d);

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
        Assert.That(receiver.ActiveHandleCount, Is.Zero);
        Assert.That(controller.HasPresentationRequest, Is.False);
    }

    [Test]
    public void DirectorStopReleasesTheRequest()
    {
        BuildTimeline(CreateTarget("Focus", Vector3.zero), 0);
        Evaluate(1.0d);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));

        director.Stop();
        TickReceiver();

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
        Assert.That(receiver.ActiveHandleCount, Is.Zero);
    }

    [Test]
    public void GraphDestructionReleasesTheRequest()
    {
        BuildTimeline(CreateTarget("Focus", Vector3.zero), 0);
        Evaluate(1.0d);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));

        if (director.playableGraph.IsValid())
        {
            director.playableGraph.Destroy();
        }

        TickReceiver();

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
    }

    [Test]
    public void APlayingDirectorKeepsItsRequestAcrossReceiverTicks()
    {
        BuildTimeline(CreateTarget("Focus", Vector3.zero), 0);
        Evaluate(1.0d);
        director.Play();
        director.time = 1.0d;
        director.Evaluate();

        TickReceiver();

        Assert.That(director.state, Is.EqualTo(PlayState.Playing));
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1),
            "A playing director (including one paused by timeScale) must keep its request.");
    }

    [Test]
    public void DisablingTheReceiverReleasesEveryRequestItOwns()
    {
        BuildTimeline(CreateTarget("Focus", Vector3.zero), 0);
        Evaluate(1.0d);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));

        Invoke(receiver, "OnDisable");

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
        Assert.That(receiver.ActiveHandleCount, Is.Zero);
    }

    [Test]
    public void SceneUnloadReleasesTimelineOwnedSceneRequests()
    {
        BuildTimeline(CreateTarget("Focus", Vector3.zero), 0);
        Evaluate(1.0d);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));

        Invoke(cameras, "OnSceneUnloaded", UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
        Assert.That(controller.HasPresentationRequest, Is.False);
    }

    [Test]
    public void OverlappingClipsResolveDeterministicallyToASingleRequest()
    {
        Transform first = CreateTarget("First", new Vector3(-10f, 0f, 0f));
        Transform second = CreateTarget("Second", new Vector3(10f, 0f, 0f));

        timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        CameraPresentationTrack track = timeline.CreateTrack<CameraPresentationTrack>(null, "Camera Presentation");
        CreateClip(track, first, 0d, 2d, CameraPresentationMode.FocusTarget, priority: 1);
        CreateClip(track, second, 1d, 2d, CameraPresentationMode.FocusTarget, priority: 2);

        director.playableAsset = timeline;
        director.SetGenericBinding(track, receiver);
        director.RebuildGraph();

        // Outside the overlap each clip resolves to its own authored priority.
        Evaluate(0.5d);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));
        Assert.That(controller.PresentationPriority, Is.EqualTo(1));

        Evaluate(2.5d);
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));
        Assert.That(controller.PresentationPriority, Is.EqualTo(2));

        // Inside the overlap exactly one request survives, and the resolution is stable: the same
        // evaluation time always produces the same winner.
        Evaluate(1.5d);
        int firstPass = controller.PresentationPriority;
        long firstId = cameras.SelectedPresentationId;
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1), "Overlap must not create a second request.");

        Evaluate(0.5d);
        Evaluate(1.5d);

        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));
        Assert.That(controller.PresentationPriority, Is.EqualTo(firstPass));
        Assert.That(cameras.SelectedPresentationId, Is.EqualTo(firstId));
    }

    [Test]
    public void ClipEaseDrivesTheRequestWeightInsteadOfChurningHandles()
    {
        timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        CameraPresentationTrack track = timeline.CreateTrack<CameraPresentationTrack>(null, "Camera Presentation");
        TimelineClip clip = CreateClip(
            track,
            CreateTarget("Focus", new Vector3(20f, 0f, 0f)),
            0d,
            2d,
            CameraPresentationMode.FocusTarget,
            priority: 0);
        clip.easeInDuration = 1d;

        director.playableAsset = timeline;
        director.SetGenericBinding(track, receiver);
        director.RebuildGraph();

        Evaluate(0.5d);
        float midEase = controller.PresentationSettings.ResolvedWeight;

        Evaluate(1.5d);
        float full = controller.PresentationSettings.ResolvedWeight;

        Assert.That(midEase, Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(full, Is.EqualTo(1f).Within(0.001f));
        Assert.That(cameras.ActivePresentationCount, Is.EqualTo(1));
    }

    [Test]
    public void MissingReceiverBindingProducesNoRequestAndDoesNotThrow()
    {
        timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        CameraPresentationTrack track = timeline.CreateTrack<CameraPresentationTrack>(null, "Camera Presentation");
        CreateClip(track, CreateTarget("Focus", Vector3.zero), 0d, 2d, CameraPresentationMode.FocusTarget, 0);

        director.playableAsset = timeline;
        director.ClearGenericBinding(track);
        director.RebuildGraph();

        Assert.DoesNotThrow(() => Evaluate(1.0d));
        Assert.That(cameras.ActivePresentationCount, Is.Zero);
    }

    [Test]
    public void UnresolvedExposedTargetLeavesNoRequestForATargetDrivenClip()
    {
        timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        CameraPresentationTrack track = timeline.CreateTrack<CameraPresentationTrack>(null, "Camera Presentation");
        TimelineClip clip = track.CreateClip<CameraPresentationClip>();
        clip.start = 0d;
        clip.duration = 2d;

        CameraPresentationClip asset = (CameraPresentationClip)clip.asset;
        asset.settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
        asset.targets = new ExposedReference<Transform>[1];
        asset.targets[0].exposedName = new PropertyName("UnboundTestTarget");

        director.playableAsset = timeline;
        director.SetGenericBinding(track, receiver);
        director.RebuildGraph();

        Evaluate(1.0d);

        Assert.That(cameras.ActivePresentationCount, Is.Zero);
        Assert.That(controller.HasPresentationRequest, Is.False);
    }

    // --------------------------------------------------------------------------------- helpers

    private void BuildTimeline(Transform focus, int priority)
    {
        timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        CameraPresentationTrack track = timeline.CreateTrack<CameraPresentationTrack>(null, "Camera Presentation");
        CreateClip(track, focus, 0d, 2d, CameraPresentationMode.FocusTarget, priority);

        director.playableAsset = timeline;
        director.SetGenericBinding(track, receiver);
        director.RebuildGraph();
    }

    private TimelineClip CreateClip(
        CameraPresentationTrack track,
        Transform focus,
        double start,
        double duration,
        CameraPresentationMode mode,
        int priority)
    {
        TimelineClip clip = track.CreateClip<CameraPresentationClip>();
        clip.start = start;
        clip.duration = duration;

        CameraPresentationClip asset = (CameraPresentationClip)clip.asset;
        CameraPresentationSettings settings = CameraPresentationSettings.Default(mode);
        settings.autoZoom = false;
        settings.authoredZoom = 1f;
        asset.settings = settings;
        asset.priority = priority;
        asset.targets = new ExposedReference<Transform>[1];
        asset.targets[0].exposedName = new PropertyName($"TestTarget_{focus.name}_{start}_{priority}");
        director.SetReferenceValue(asset.targets[0].exposedName, focus);
        return clip;
    }

    private void Evaluate(double time)
    {
        director.time = time;
        director.Evaluate();
    }

    // Stands in for the receiver's own LateUpdate, which is the backstop that releases requests a
    // stopped or destroyed director can no longer refresh.
    private void TickReceiver()
    {
        Invoke(receiver, "LateUpdate");
    }

    private Transform CreateTarget(string name, Vector3 position)
    {
        GameObject go = new GameObject(name);
        go.transform.position = position;
        temporaries.Add(go);
        return go.transform;
    }

    private void EnsureCamerasInstance()
    {
        if (GameCameras.Instance == cameras)
        {
            return;
        }

        if (GameCameras.Instance != null && GameCameras.Instance != cameras)
        {
            Object.DestroyImmediate(GameCameras.Instance.gameObject);
        }

        typeof(GameCameras)
            .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, cameras);
    }

    private static object Invoke(object targetObject, string method, params object[] arguments)
    {
        foreach (MethodInfo candidate in targetObject.GetType().GetMethods(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (candidate.Name == method && candidate.GetParameters().Length == arguments.Length)
            {
                return candidate.Invoke(targetObject, arguments);
            }
        }

        Assert.Fail($"Method '{method}' was not found on {targetObject.GetType().Name}.");
        return null;
    }

    private static void SetField(object targetObject, string field, object value)
    {
        targetObject.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SetValue(targetObject, value);
    }
}
