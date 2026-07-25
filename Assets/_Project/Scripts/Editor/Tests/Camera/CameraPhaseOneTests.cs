using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CameraPhaseOneTests
{
    private GameObject hero;
    private GameObject targetObject;
    private GameObject controllerObject;
    private CameraTarget target;
    private CameraController controller;

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
    }

    [TearDown]
    public void TearDown()
    {
        if (GameCameras.Instance != null)
        {
            Object.DestroyImmediate(GameCameras.Instance.gameObject);
        }

        Object.DestroyImmediate(controllerObject);
        Object.DestroyImmediate(targetObject);
        Object.DestroyImmediate(hero);
    }

    [Test]
    public void HighestPriorityWinsRegardlessOfEntryOrder()
    {
        CameraLockArea high = CreateLock("High", 10);
        CameraLockArea low = CreateLock("Low", 1);
        try
        {
            controller.EnterLockArea(high);
            controller.EnterLockArea(low);

            Assert.That(controller.CurrentLockArea, Is.SameAs(high));
        }
        finally
        {
            DestroyLocks(high, low);
        }
    }

    [Test]
    public void NewestEqualPriorityWins()
    {
        CameraLockArea first = CreateLock("First", 5);
        CameraLockArea second = CreateLock("Second", 5);
        try
        {
            controller.EnterLockArea(first);
            long firstSequence = controller.CurrentLockEntrySequence;
            controller.EnterLockArea(second);

            Assert.That(controller.CurrentLockArea, Is.SameAs(second));
            Assert.That(controller.CurrentLockEntrySequence, Is.GreaterThan(firstSequence));
        }
        finally
        {
            DestroyLocks(first, second);
        }
    }

    [Test]
    public void DuplicateEntryIsIdempotentAndDoesNotChangeSequence()
    {
        CameraLockArea area = CreateLock("Area", 5);
        try
        {
            controller.EnterLockArea(area);
            long sequence = controller.CurrentLockEntrySequence;
            controller.EnterLockArea(area);

            Assert.That(controller.ActiveLockCount, Is.EqualTo(1));
            Assert.That(controller.CurrentLockEntrySequence, Is.EqualTo(sequence));
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [Test]
    public void ActiveRemovalRestoresFallback()
    {
        CameraLockArea fallback = CreateLock("Fallback", 1);
        CameraLockArea active = CreateLock("Active", 2);
        try
        {
            controller.EnterLockArea(fallback);
            controller.EnterLockArea(active);
            controller.ExitLockArea(active);

            Assert.That(controller.CurrentLockArea, Is.SameAs(fallback));
        }
        finally
        {
            DestroyLocks(fallback, active);
        }
    }

    [Test]
    public void NonActiveRemovalDoesNotChangeSelectionOrSequence()
    {
        CameraLockArea active = CreateLock("Active", 2);
        CameraLockArea lower = CreateLock("Lower", 1);
        try
        {
            controller.EnterLockArea(active);
            controller.EnterLockArea(lower);
            long sequence = controller.CurrentLockEntrySequence;
            controller.ExitLockArea(lower);

            Assert.That(controller.CurrentLockArea, Is.SameAs(active));
            Assert.That(controller.CurrentLockEntrySequence, Is.EqualTo(sequence));
        }
        finally
        {
            DestroyLocks(active, lower);
        }
    }

    [Test]
    public void DisableAndDestroyCannotLeaveStaleRegistration()
    {
        CameraLockArea fallback = CreateLock("Fallback", 1);
        CameraLockArea active = CreateLock("Active", 2);
        controller.EnterLockArea(fallback);
        controller.EnterLockArea(active);

        active.gameObject.SetActive(false);
        Assert.That(controller.CurrentLockArea, Is.SameAs(fallback));

        Object.DestroyImmediate(fallback.gameObject);
        Assert.That(controller.CurrentLockArea, Is.Null);
        Assert.That(controller.ActiveLockCount, Is.Zero);
    }

    [Test]
    public void SceneResetClearsRegistrations()
    {
        CameraLockArea area = CreateLock("Area", 1);
        try
        {
            controller.EnterLockArea(area);
            controller.SceneInit();

            Assert.That(controller.ActiveLockCount, Is.Zero);
            Assert.That(controller.CurrentLockArea, Is.Null);
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [TestCase(true, false, -30f, -20f, 5f, -20f)]
    [TestCase(false, true, -30f, -20f, -26f, -7f)]
    [TestCase(true, true, -30f, -20f, 5f, -7f)]
    public void AxisLocksUseOnlyTheirOwnedLegalAxis(
        bool lockX,
        bool lockY,
        float requestedX,
        float requestedY,
        float expectedMinimumX,
        float expectedMinimumY)
    {
        CameraBoundsVolume room = CreateBounds(new Vector2(80f, 60f), Vector2.zero);
        CameraLockArea area = CreateLock("Axis Lock", 1, new Vector2(40f, 30f), new Vector2(20f, 0f), lockX, lockY);
        try
        {
            controller.SetBoundsVolume(room);
            controller.EnterLockArea(area);
            target.transform.position = new Vector3(requestedX, requestedY, 0f);

            Vector3 destination = ComputeDestination();

            Assert.That(destination.x, Is.GreaterThanOrEqualTo(expectedMinimumX - 1f));
            Assert.That(destination.y, Is.GreaterThanOrEqualTo(expectedMinimumY - 1f));
            if (!lockY)
            {
                Assert.That(destination.y, Is.EqualTo(requestedY).Within(0.01f));
            }
        }
        finally
        {
            Object.DestroyImmediate(room.gameObject);
            DestroyLocks(area);
        }
    }

    [Test]
    public void LockOutsideRoomCollapsesToNearestLegalRoomPoint()
    {
        CameraBoundsVolume room = CreateBounds(new Vector2(80f, 60f), Vector2.zero);
        CameraLockArea area = CreateLock("Outside", 1, new Vector2(20f, 20f), new Vector2(100f, 0f));
        try
        {
            controller.SetBoundsVolume(room);
            controller.EnterLockArea(area);
            target.transform.position = new Vector3(100f, 0f, 0f);

            Vector3 destination = ComputeDestination();
            float halfWidth = GetFrustumHalfWidth();

            Assert.That(destination.x, Is.EqualTo(40f - halfWidth).Within(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(room.gameObject);
            DestroyLocks(area);
        }
    }

    [Test]
    public void RegionSmallerThanViewportUsesAuthoredCenter()
    {
        CameraBoundsVolume room = CreateBounds(new Vector2(2f, 2f), new Vector2(3f, 4f));
        try
        {
            controller.SetBoundsVolume(room);
            target.transform.position = new Vector3(100f, 100f, 0f);

            Vector3 destination = ComputeDestination();

            Assert.That(destination.x, Is.EqualTo(3f).Within(0.01f));
            Assert.That(destination.y, Is.EqualTo(4f).Within(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(room.gameObject);
        }
    }

    [Test]
    public void SwitchingAxisAndUnlockingClearsOldConstraint()
    {
        CameraBoundsVolume room = CreateBounds(new Vector2(80f, 60f), Vector2.zero);
        CameraLockArea xOnly = CreateLock("X", 1, new Vector2(40f, 30f), new Vector2(20f, 0f), true, false);
        CameraLockArea yOnly = CreateLock("Y", 2, new Vector2(40f, 30f), new Vector2(20f, 0f), false, true);
        try
        {
            controller.SetBoundsVolume(room);
            controller.EnterLockArea(xOnly);
            target.transform.position = new Vector3(-30f, -20f, 0f);
            Assert.That(ComputeDestination().y, Is.EqualTo(-20f).Within(0.01f));

            controller.EnterLockArea(yOnly);
            Assert.That(ComputeDestination().x, Is.LessThan(0f));

            controller.ExitLockArea(yOnly);
            controller.ExitLockArea(xOnly);
            Assert.That(controller.Mode, Is.EqualTo(CameraMode.Follow));
        }
        finally
        {
            Object.DestroyImmediate(room.gameObject);
            DestroyLocks(xOnly, yOnly);
        }
    }

    [Test]
    public void LookOverrideCanBeExplicitlyAuthoredAtWorldZero()
    {
        CameraLockArea area = CreateLock("Look", 1);
        try
        {
            SetField(area, "preventLookUp", true);
            SetField(area, "overrideLookYMax", true);
            SetField(area, "lookYMax", 0f);

            Assert.That(area.HasLookYMax, Is.True);
            Assert.That(area.LookYMax, Is.Zero);
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [Test]
    public void LookOverrideDistinguishesUnassignedFromZeroAndClearsOnSwitch()
    {
        CameraLockArea authoredZero = CreateLock("Zero", 2);
        CameraLockArea none = CreateLock("None", 3);
        try
        {
            SetField(authoredZero, "preventLookDown", true);
            SetField(authoredZero, "overrideLookYMin", true);
            SetField(authoredZero, "lookYMin", 0f);
            SetField(none, "preventLookDown", true);

            Assert.That(authoredZero.HasLookYMin, Is.True);
            Assert.That(none.HasLookYMin, Is.False);

            controller.EnterLockArea(authoredZero);
            controller.EnterLockArea(none);
            Assert.That(controller.CurrentLockArea.HasLookYMin, Is.False);
        }
        finally
        {
            DestroyLocks(authoredZero, none);
        }
    }

    [Test]
    public void LockChangesUnderFreezeAndFinalReleaseRestoresCurrentFraming()
    {
        GameCameras cameras = CreateGameCameras();
        CameraLockArea first = CreateLock("First", 1);
        CameraLockArea second = CreateLock("Second", 2);
        try
        {
            controller.EnterLockArea(first);
            CameraRequestHandle handle = cameras.AcquireFreeze(CameraFreezeKind.Hard, -1f, this, CameraRequestLifetime.Persistent);
            controller.EnterLockArea(second);

            Assert.That(controller.Mode, Is.EqualTo(CameraMode.Frozen));
            Assert.That(controller.CurrentLockArea, Is.SameAs(second));

            handle.Release();
            Assert.That(controller.Mode, Is.EqualTo(CameraMode.Locked));
            Assert.That(controller.CurrentLockArea, Is.SameAs(second));
        }
        finally
        {
            DestroyLocks(first, second);
        }
    }

    [Test]
    public void TwoIndependentFreezesReleaseOnlyTheirOwnHandle()
    {
        GameCameras cameras = CreateGameCameras();
        CameraRequestHandle first = cameras.AcquireFreeze(CameraFreezeKind.Soft, -1f, new object(), CameraRequestLifetime.Persistent);
        CameraRequestHandle second = cameras.AcquireFreeze(CameraFreezeKind.Hard, -1f, new object(), CameraRequestLifetime.Persistent);

        first.Release();
        Assert.That(cameras.ActiveFreezeCount, Is.EqualTo(1));
        Assert.That(controller.Mode, Is.EqualTo(CameraMode.Frozen));

        second.Release();
        second.Release();
        Assert.That(cameras.ActiveFreezeCount, Is.Zero);
        Assert.That(controller.Mode, Is.EqualTo(CameraMode.Follow));
    }

    [Test]
    public void ExpiringRequestAndDestroyedSourceRemoveOnlyThemselves()
    {
        GameCameras cameras = CreateGameCameras();
        GameObject source = new GameObject("Scene Source");
        CameraRequestHandle timed = cameras.AcquireFreeze(CameraFreezeKind.Soft, 10f, source, CameraRequestLifetime.Scene);
        CameraRequestHandle indefinite = cameras.AcquireFreeze(CameraFreezeKind.Hard, -1f, this, CameraRequestLifetime.Persistent);
        try
        {
            ExpireFirstFreeze(cameras);
            Invoke(cameras, "Update");
            Assert.That(cameras.ActiveFreezeCount, Is.EqualTo(1));

            indefinite.Release();
            Assert.That(cameras.ActiveFreezeCount, Is.Zero);
        }
        finally
        {
            timed.Release();
            indefinite.Release();
            Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void DestroyedUnitySourceCleansOnlyItsOwnRequest()
    {
        GameCameras cameras = CreateGameCameras();
        GameObject source = new GameObject("Destroyed Source");
        cameras.AcquireFreeze(CameraFreezeKind.Soft, -1f, source, CameraRequestLifetime.Scene);
        CameraRequestHandle persistent = cameras.AcquireFreeze(
            CameraFreezeKind.Hard,
            -1f,
            this,
            CameraRequestLifetime.Persistent);

        Object.DestroyImmediate(source);
        Invoke(cameras, "Update");

        Assert.That(cameras.ActiveFreezeCount, Is.EqualTo(1));
        Assert.That(controller.Mode, Is.EqualTo(CameraMode.Frozen));
        persistent.Release();
    }

    [Test]
    public void TwoTimedRequestsExpireIndependently()
    {
        GameCameras cameras = CreateGameCameras();
        cameras.AcquireFreeze(CameraFreezeKind.Soft, 10f, new object(), CameraRequestLifetime.Persistent);
        CameraRequestHandle later = cameras.AcquireFreeze(
            CameraFreezeKind.Hard,
            20f,
            new object(),
            CameraRequestLifetime.Persistent);

        ExpireFirstFreeze(cameras);
        Invoke(cameras, "Update");

        Assert.That(cameras.ActiveFreezeCount, Is.EqualTo(1));
        Assert.That(controller.Mode, Is.EqualTo(CameraMode.Frozen));
        later.Release();
    }

    [Test]
    public void SceneCleanupRemovesSceneRequestButPreservesTransitionLifetime()
    {
        GameCameras cameras = CreateGameCameras();
        Scene scene = SceneManager.GetActiveScene();
        GameObject source = new GameObject("Scene Request Source");
        cameras.AcquireFreeze(CameraFreezeKind.Soft, -1f, source, CameraRequestLifetime.Scene);
        CameraRequestHandle transition = cameras.AcquireFreeze(
            CameraFreezeKind.Hard,
            -1f,
            new object(),
            CameraRequestLifetime.Persistent);
        try
        {
            Invoke(cameras, "OnSceneUnloaded", scene);

            Assert.That(cameras.ActiveFreezeCount, Is.EqualTo(1));
            Assert.That(controller.Mode, Is.EqualTo(CameraMode.Frozen));
        }
        finally
        {
            transition.Release();
            Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void ExitingSelectedLockDuringFreezeRestoresFollowOnRelease()
    {
        GameCameras cameras = CreateGameCameras();
        CameraLockArea area = CreateLock("Area", 1);
        try
        {
            controller.EnterLockArea(area);
            CameraRequestHandle freeze = cameras.AcquireFreeze(
                CameraFreezeKind.Hard,
                -1f,
                this,
                CameraRequestLifetime.Persistent);
            controller.ExitLockArea(area);

            Assert.That(controller.Mode, Is.EqualTo(CameraMode.Frozen));
            freeze.Release();
            Assert.That(controller.Mode, Is.EqualTo(CameraMode.Follow));
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [Test]
    public void FreeModeEndsIntoCurrentUnderlyingLock()
    {
        CameraLockArea area = CreateLock("Area", 1);
        try
        {
            controller.StartFreeMode();
            controller.EnterLockArea(area);
            Assert.That(controller.Mode, Is.EqualTo(CameraMode.Free));

            controller.EndFreeMode();
            Assert.That(controller.Mode, Is.EqualTo(CameraMode.Locked));
            Assert.That(target.Mode, Is.EqualTo(CameraTarget.TargetMode.LockZone));
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    private GameCameras CreateGameCameras()
    {
        GameObject root = new GameObject("Game Cameras");
        GameCameras cameras = root.AddComponent<GameCameras>();
        SetField(cameras, "cameraController", controller);
        SetField(cameras, "cameraTarget", target);
        return cameras;
    }

    private CameraBoundsVolume CreateBounds(Vector2 size, Vector2 center)
    {
        GameObject go = new GameObject("Bounds");
        go.transform.position = center;
        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = size;
        return go.AddComponent<CameraBoundsVolume>();
    }

    private CameraLockArea CreateLock(
        string name,
        int priority,
        Vector2? size = null,
        Vector2? center = null,
        bool lockX = true,
        bool lockY = true)
    {
        GameObject go = new GameObject(name);
        go.transform.position = center ?? Vector2.zero;
        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = size ?? new Vector2(40f, 30f);
        CameraLockArea area = go.AddComponent<CameraLockArea>();
        SetField(area, "priority", priority);
        SetField(area, "lockX", lockX);
        SetField(area, "lockY", lockY);
        return area;
    }

    private Vector3 ComputeDestination()
    {
        return (Vector3)Invoke(controller, "ComputeDestination");
    }

    private float GetFrustumHalfWidth()
    {
        Camera camera = controllerObject.GetComponent<Camera>();
        float halfHeight = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)
            * Mathf.Abs(controller.transform.position.z);
        return halfHeight * camera.aspect;
    }

    private static void ExpireFirstFreeze(GameCameras cameras)
    {
        IList registrations = (IList)GetField(cameras, "freezeRegistrations");
        object registration = registrations[0];
        SetField(registration, "ExpiresAt", 0.0001f);
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

    private static object GetField(object targetObject, string field)
    {
        return targetObject.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .GetValue(targetObject);
    }

    private static void SetField(object targetObject, string field, object value)
    {
        targetObject.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SetValue(targetObject, value);
    }

    private static void DestroyLocks(params CameraLockArea[] areas)
    {
        for (int i = 0; i < areas.Length; i++)
        {
            if (areas[i] != null)
            {
                Object.DestroyImmediate(areas[i].gameObject);
            }
        }
    }
}
