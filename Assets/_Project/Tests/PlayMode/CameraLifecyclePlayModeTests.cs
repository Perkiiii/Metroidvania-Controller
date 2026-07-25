using System;
using System.Collections;
using System.Collections.Generic;
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

    private static Type RuntimeType(string name)
    {
        Type type = Type.GetType($"{name}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Runtime type '{name}' was not found in Assembly-CSharp.");
        return type;
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
