using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CameraLifecyclePlayModeTests
{
    private Type controllerType;
    private Type targetType;
    private Type camerasType;
    private Type lockType;
    private Type boundsType;
    private Type freezeKindType;
    private Type lifetimeType;
    private GameObject hero;
    private GameObject camerasRoot;
    private Component controller;
    private Component target;
    private Component cameras;
    private readonly List<GameObject> createdVolumes = new List<GameObject>();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        controllerType = RuntimeType("CameraController");
        targetType = RuntimeType("CameraTarget");
        camerasType = RuntimeType("GameCameras");
        lockType = RuntimeType("CameraLockArea");
        boundsType = RuntimeType("CameraBoundsVolume");
        freezeKindType = RuntimeType("CameraFreezeKind");
        lifetimeType = RuntimeType("CameraRequestLifetime");

        hero = new GameObject("PlayMode Camera Hero") { tag = "Player" };
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
        controllerObject.AddComponent<Camera>();
        controller = controllerObject.AddComponent(controllerType);
        SetField(controller, "cameraTarget", target);
        SetField(cameras, "cameraController", controller);
        SetField(cameras, "cameraTarget", target);

        Invoke(target, "SceneInit");
        Invoke(controller, "SceneInit");
        Invoke(cameras, "OnSceneInit", SceneManager.GetActiveScene());
        Physics2D.SyncTransforms();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int i = 0; i < createdVolumes.Count; i++)
        {
            if (createdVolumes[i] != null) UnityEngine.Object.Destroy(createdVolumes[i]);
        }
        createdVolumes.Clear();
        if (camerasRoot != null) UnityEngine.Object.Destroy(camerasRoot);
        if (hero != null) UnityEngine.Object.Destroy(hero);

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name.StartsWith("SampleScene", StringComparison.Ordinal))
        {
            Scene cleanupScene = SceneManager.CreateScene("Camera Lifecycle Test Cleanup");
            SceneManager.SetActiveScene(cleanupScene);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(activeScene);
            while (unload != null && !unload.isDone)
            {
                yield return null;
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator EnablingLockAroundAlreadyPositionedPlayerRegistersImmediately()
    {
        Component area = CreateLock("Immediate Lock", Vector2.zero, new Vector2(8f, 8f), 1);
        Physics2D.SyncTransforms();
        yield return null;

        Assert.That(GetProperty(controller, "CurrentLockArea"), Is.SameAs(area));
        Assert.That(GetProperty(controller, "ActiveLockCount"), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator EnablingBoundsAroundAlreadyPositionedPlayerRegistersBeforeCameraMove()
    {
        Component bounds = CreateBounds("Immediate Bounds", Vector2.zero, new Vector2(40f, 30f));
        Physics2D.SyncTransforms();
        Invoke(cameras, "RefreshSceneOverlaps");
        yield return null;

        Assert.That(GetProperty(controller, "CurrentBoundsVolume"), Is.SameAs(bounds));
        Assert.That(GetProperty(controller, "ActiveBoundsCount"), Is.EqualTo(1));
        Assert.That((string)GetProperty(controller, "Mode").ToString(), Is.EqualTo("Follow"));
    }

    [UnityTest]
    public IEnumerator PhysicalEntryExitAndOverlappingFallbackAreDeterministic()
    {
        Component fallback = CreateLock("Fallback", new Vector2(10f, 0f), new Vector2(8f, 8f), 1);
        Component priority = CreateLock("Priority", new Vector2(10f, 0f), new Vector2(8f, 8f), 2);

        hero.transform.position = new Vector2(10f, 0f);
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        Assert.That(GetProperty(controller, "CurrentLockArea"), Is.SameAs(priority));

        priority.gameObject.SetActive(false);
        yield return null;
        Assert.That(GetProperty(controller, "CurrentLockArea"), Is.SameAs(fallback));

        hero.transform.position = Vector2.zero;
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        Assert.That(GetProperty(controller, "CurrentLockArea"), Is.Null);
    }

    [UnityTest]
    public IEnumerator LockChangesRemainUnderlyingWhileFrozen()
    {
        object hard = Enum.Parse(freezeKindType, "Hard");
        object persistent = Enum.Parse(lifetimeType, "Persistent");
        object handle = Invoke(cameras, "AcquireFreeze", hard, -1f, this, persistent);
        Component area = CreateLock("Frozen Lock", Vector2.zero, new Vector2(8f, 8f), 1);
        Physics2D.SyncTransforms();
        yield return null;

        Assert.That(GetProperty(controller, "Mode").ToString(), Is.EqualTo("Frozen"));
        Assert.That(GetProperty(controller, "CurrentLockArea"), Is.SameAs(area));

        Invoke(handle, "Release");
        yield return null;
        Assert.That(GetProperty(controller, "Mode").ToString(), Is.EqualTo("Locked"));
    }

    [UnityTest]
    public IEnumerator SceneRebindClearsOldRegistrationAndRefreshesCurrentOverlapOnce()
    {
        Component area = CreateLock("Rebind Lock", Vector2.zero, new Vector2(8f, 8f), 1);
        Physics2D.SyncTransforms();
        yield return null;
        Assert.That(GetProperty(controller, "ActiveLockCount"), Is.EqualTo(1));

        Invoke(cameras, "RebindForSceneEntry");
        yield return null;
        Assert.That(GetProperty(controller, "CurrentLockArea"), Is.SameAs(area));
        Assert.That(GetProperty(controller, "ActiveLockCount"), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator SceneEntryReadinessRegistersGeometryAndPositionsBeforeCompletion()
    {
        hero.transform.position = new Vector2(16f, 3f);
        Component bounds = CreateBounds("Entry Bounds", hero.transform.position, new Vector2(40f, 30f));
        Component area = CreateLock("Entry Lock", hero.transform.position, new Vector2(12f, 10f), 2);
        Physics2D.SyncTransforms();

        object readiness = null;
        Delegate readinessCallback = CreateBoxingAction(
            RuntimeType("CameraSceneEntryReadiness"),
            value => readiness = value);
        IEnumerator routine = (IEnumerator)Invoke(
            cameras,
            "RebindAndPositionForSceneEntry",
            "PlayModeReadinessRoom",
            0.5f,
            readinessCallback);
        yield return routine;

        Assert.That(readiness, Is.Not.Null);
        Assert.That((bool)GetProperty(readiness, "IsReady"), Is.True);
        Assert.That((bool)GetProperty(readiness, "UsedFallback"), Is.False);
        Assert.That(GetProperty(controller, "CurrentBoundsVolume"), Is.SameAs(bounds));
        Assert.That(GetProperty(controller, "CurrentLockArea"), Is.SameAs(area));
        Assert.That((bool)GetProperty(controller, "LastApplicationWasImmediate"), Is.True);
        Assert.That((bool)GetProperty(controller, "IsTransitioning"), Is.False);

        Vector3 rendered = (Vector3)GetProperty(controller, "RenderedPosition");
        Vector3 destination = (Vector3)GetProperty(controller, "CurrentDestination");
        Assert.That(rendered, Is.EqualTo(destination));
    }

    [UnityTest]
    public IEnumerator TransitionFreezeReleaseAfterReadinessHasNoLateOverrideSnap()
    {
        object handle = Invoke(cameras, "FreezeForSceneTransition", this);
        object readiness = null;
        Delegate readinessCallback = CreateBoxingAction(
            RuntimeType("CameraSceneEntryReadiness"),
            value => readiness = value);
        IEnumerator routine = (IEnumerator)Invoke(
            cameras,
            "RebindAndPositionForSceneEntry",
            "PlayModeFreezeRoom",
            0.5f,
            readinessCallback);
        yield return routine;
        Assert.That((bool)GetProperty(readiness, "IsReady"), Is.True);

        Invoke(handle, "Release");
        yield return null;

        Assert.That(GetProperty(cameras, "ActiveFreezeCount"), Is.Zero);
        Assert.That(GetProperty(controller, "CurrentTransitionCause").ToString(), Is.EqualTo("None"));
        Assert.That((bool)GetProperty(controller, "IsTransitioning"), Is.False);
    }

    [UnityTest]
    public IEnumerator HorizontalRoomTransitionsRevealOnlyAfterReadinessInBothDirections()
    {
        GameObject fadeObject = new GameObject("Transition Fade");
        fadeObject.transform.SetParent(camerasRoot.transform);
        CanvasGroup canvasGroup = fadeObject.AddComponent<CanvasGroup>();
        Component cameraFade = fadeObject.AddComponent(RuntimeType("CameraFade"));
        SetField(cameras, "fade", cameraFade);

        GameObject managerObject = new GameObject("Transition Test Game Manager");
        Component gameManager = managerObject.AddComponent(RuntimeType("GameManager"));

        yield return LoadSingle("SampleScene");
        Invoke(gameManager, "OnSceneLoaded", SceneManager.GetActiveScene(), LoadSceneMode.Single);
        yield return RunAndVerifyHorizontalTransition(
            gameManager,
            canvasGroup,
            "SampleScene2",
            "68be6e2e-2440-4f65-b987-5bd652a097c8");
        yield return RunAndVerifyHorizontalTransition(
            gameManager,
            canvasGroup,
            "SampleScene",
            "c6a379cc-35f6-449f-be7f-f853733735aa");

        Scene cleanupScene = SceneManager.CreateScene("Camera Transition Test Cleanup");
        SceneManager.SetActiveScene(cleanupScene);
        Scene gameplayScene = SceneManager.GetSceneByName("SampleScene");
        if (gameplayScene.IsValid() && gameplayScene.isLoaded)
        {
            AsyncOperation unload = SceneManager.UnloadSceneAsync(gameplayScene);
            while (unload != null && !unload.isDone)
            {
                yield return null;
            }
        }

        UnityEngine.Object.Destroy(managerObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator SampleScene4ArenaLockAcquiresAndReleasesWithoutStaleRegistration()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync("SampleScene4", LoadSceneMode.Additive);
        Assert.That(load, Is.Not.Null, "SampleScene4 must be enabled in Build Settings.");
        while (!load.isDone)
        {
            yield return null;
        }

        Scene scene = SceneManager.GetSceneByName("SampleScene4");
        Component arenaLock = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Component[] locks = root.GetComponentsInChildren(lockType, true);
            if (locks.Length > 0)
            {
                arenaLock = locks[0];
                break;
            }
        }

        Assert.That(arenaLock, Is.Not.Null, "SampleScene4 must contain its authored ArenaCameraLock.");
        Assert.That(arenaLock.gameObject.activeSelf, Is.False, "ArenaCameraLock must start inactive.");

        arenaLock.gameObject.SetActive(true);
        Invoke(controller, "EnterLockArea", arenaLock);
        Invoke(controller, "EnterLockArea", arenaLock);
        yield return null;

        Assert.That(GetProperty(controller, "CurrentLockArea"), Is.SameAs(arenaLock));
        Assert.That(GetProperty(controller, "ActiveLockCount"), Is.EqualTo(1));

        arenaLock.gameObject.SetActive(false);
        yield return null;
        Assert.That(GetProperty(controller, "ActiveLockCount"), Is.Zero);

        AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
        while (unload != null && !unload.isDone)
        {
            yield return null;
        }
    }

    private Component CreateLock(string name, Vector2 position, Vector2 size, int priority)
    {
        GameObject go = new GameObject(name);
        createdVolumes.Add(go);
        go.transform.position = position;
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = true;
        Component area = go.AddComponent(lockType);
        SetField(area, "priority", priority);
        return area;
    }

    private Component CreateBounds(string name, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name);
        createdVolumes.Add(go);
        go.transform.position = position;
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = true;
        return go.AddComponent(boundsType);
    }

    private IEnumerator RunAndVerifyHorizontalTransition(
        Component gameManager,
        CanvasGroup canvasGroup,
        string targetScene,
        string destinationGuid)
    {
        Type requestType = RuntimeType("SceneTransitionRequest");
        Type kindType = RuntimeType("SceneTransitionKind");
        object request = Activator.CreateInstance(
            requestType,
            targetScene,
            destinationGuid,
            null,
            Enum.Parse(kindType, "Gate"),
            $"PlayMode {targetScene} transition");

        bool started = (bool)Invoke(gameManager, "BeginSceneTransition", request);
        Assert.That(started, Is.True);

        float deadline = Time.realtimeSinceStartup + 8f;
        bool sawVisibleFrame = false;
        while ((bool)GetProperty(gameManager, "IsSceneTransitioning"))
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), $"Transition to {targetScene} timed out.");

            object trace = GetProperty(gameManager, "ActiveSceneTransitionTimeline");
            if (canvasGroup.alpha < 0.999f
                && trace != null
                && TraceContains(trace, "SceneLoaded"))
            {
                sawVisibleFrame = true;
                Assert.That(TraceContains(trace, "CameraReady"), Is.True, "Destination became visible before camera readiness.");
                Assert.That((bool)GetProperty(controller, "LastApplicationWasImmediate"), Is.True);
                Assert.That((Vector3)GetProperty(controller, "RenderedPosition"),
                    Is.EqualTo((Vector3)GetProperty(controller, "CurrentDestination")));
            }

            yield return null;
        }

        object completedTrace = GetProperty(gameManager, "LastSceneTransitionTimeline");
        Assert.That(sawVisibleFrame, Is.True, "The transition never produced a visible reveal frame.");
        Assert.That(TraceIndex(completedTrace, "HeroPlaced"), Is.LessThan(TraceIndex(completedTrace, "CameraReady")));
        Assert.That(TraceIndex(completedTrace, "CameraReady"), Is.LessThan(TraceIndex(completedTrace, "FadeInStarted")));
        Assert.That(TraceIndex(completedTrace, "CameraReady"), Is.LessThan(TraceIndex(completedTrace, "EntryMotionStarted")));
        float fadeInStarted = TraceRealtime(completedTrace, "FadeInStarted");
        float entryMotionStarted = TraceRealtime(completedTrace, "EntryMotionStarted");
        float overlapEnded = Mathf.Min(
            TraceRealtime(completedTrace, "FadeInComplete"),
            TraceRealtime(completedTrace, "EntryMotionComplete"));
        Assert.That(entryMotionStarted, Is.EqualTo(fadeInStarted).Within(0.05f));
        Assert.That(overlapEnded, Is.GreaterThan(fadeInStarted), "Entry motion did not overlap the visible fade-in interval.");
        Assert.That(GetProperty(cameras, "ActiveFreezeCount"), Is.Zero);
        Assert.That(GetProperty(controller, "CurrentTransitionCause").ToString(), Is.EqualTo("None"));
        Assert.That((Vector3)GetProperty(controller, "RenderedPosition"),
            Is.EqualTo((Vector3)GetProperty(controller, "CurrentDestination")));
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(targetScene));
    }

    private static IEnumerator LoadSingle(string sceneName)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        Assert.That(load, Is.Not.Null);
        while (!load.isDone)
        {
            yield return null;
        }
    }

    private static bool TraceContains(object trace, string markerName)
    {
        return TraceIndex(trace, markerName) >= 0;
    }

    private static int TraceIndex(object trace, string markerName)
    {
        object markers = GetProperty(trace, "Markers");
        int count = (int)markers.GetType().GetProperty("Count").GetValue(markers);
        PropertyInfo indexer = markers.GetType().GetProperty("Item");
        for (int i = 0; i < count; i++)
        {
            object marker = indexer.GetValue(markers, new object[] { i });
            if (marker.ToString() == markerName)
            {
                return i;
            }
        }

        return -1;
    }

    private static float TraceRealtime(object trace, string markerName)
    {
        object entries = GetProperty(trace, "Entries");
        int count = (int)entries.GetType().GetProperty("Count").GetValue(entries);
        PropertyInfo indexer = entries.GetType().GetProperty("Item");
        for (int i = 0; i < count; i++)
        {
            object entry = indexer.GetValue(entries, new object[] { i });
            object marker = entry.GetType().GetField("Marker").GetValue(entry);
            if (marker.ToString() == markerName)
            {
                return (float)entry.GetType().GetField("Realtime").GetValue(entry);
            }
        }

        Assert.Fail($"Trace marker '{markerName}' was not found.");
        return -1f;
    }

    private static Type RuntimeType(string name)
    {
        Type type = Type.GetType($"{name}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Runtime type '{name}' was not found in Assembly-CSharp.");
        return type;
    }

    private static Delegate CreateBoxingAction(Type valueType, Action<object> callback)
    {
        Type delegateType = typeof(Action<>).MakeGenericType(valueType);
        ParameterExpression value = Expression.Parameter(valueType, "value");
        MethodInfo invoke = typeof(Action<object>).GetMethod(nameof(Action<object>.Invoke));
        MethodCallExpression body = Expression.Call(
            Expression.Constant(callback),
            invoke,
            Expression.Convert(value, typeof(object)));
        return Expression.Lambda(delegateType, body, value).Compile();
    }

    private static object GetProperty(object instance, string property)
    {
        return instance.GetType()
            .GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .GetValue(instance);
    }

    private static void SetField(object instance, string field, object value)
    {
        instance.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(instance, value);
    }

    private static object Invoke(object instance, string method, params object[] arguments)
    {
        Type[] argumentTypes = new Type[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
        {
            argumentTypes[i] = arguments[i].GetType();
        }

        MethodInfo selected = null;
        foreach (MethodInfo candidate in instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            ParameterInfo[] parameters = candidate.GetParameters();
            if (candidate.Name != method || parameters.Length != arguments.Length)
            {
                continue;
            }

            bool compatible = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (!parameters[i].ParameterType.IsAssignableFrom(argumentTypes[i]))
                {
                    compatible = false;
                    break;
                }
            }

            if (compatible)
            {
                selected = candidate;
                break;
            }
        }

        Assert.That(selected, Is.Not.Null, $"Method '{method}' was not found on {instance.GetType().Name}.");
        return selected.Invoke(instance, arguments);
    }
}
