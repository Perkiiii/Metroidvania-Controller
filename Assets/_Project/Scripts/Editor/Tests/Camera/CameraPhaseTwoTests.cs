using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CameraPhaseTwoTests
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

        // SceneInit re-arms the scene-start lock timer, which intentionally forces every
        // application to Immediate. Live-transition tests clear it explicitly.
        SetField(controller, "startTimer", 0f);
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
    public void FollowToLockSelectsConfiguredSettingsAndBlends()
    {
        CameraLockArea area = CreateLock("Area", 1);
        try
        {
            controller.EnterLockArea(area);

            Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.FollowToLock));
            Assert.That(controller.IsTransitioning, Is.True);
            Assert.That(controller.LastApplicationWasImmediate, Is.False);
            Assert.That(controller.TransitionDestinationLockArea, Is.SameAs(area));
            Assert.That(controller.TransitionSourceLockArea, Is.Null);
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [Test]
    public void LockToLockSelectsConfiguredCause()
    {
        CameraLockArea first = CreateLock("First", 1);
        CameraLockArea second = CreateLock("Second", 2);
        try
        {
            controller.EnterLockArea(first);
            controller.EnterLockArea(second);

            Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.LockToLock));
            Assert.That(controller.TransitionSourceLockArea, Is.SameAs(first));
            Assert.That(controller.TransitionDestinationLockArea, Is.SameAs(second));
        }
        finally
        {
            DestroyLocks(first, second);
        }
    }

    [Test]
    public void ActiveRemovalStartsLockToLockFallbackTransition()
    {
        CameraLockArea fallback = CreateLock("Fallback", 1);
        CameraLockArea active = CreateLock("Active", 2);
        try
        {
            controller.EnterLockArea(fallback);
            controller.EnterLockArea(active);
            controller.ExitLockArea(active);

            Assert.That(controller.CurrentLockArea, Is.SameAs(fallback));
            Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.LockToLock));
            Assert.That(controller.TransitionSourceLockArea, Is.SameAs(active));
            Assert.That(controller.TransitionDestinationLockArea, Is.SameAs(fallback));
        }
        finally
        {
            DestroyLocks(fallback, active);
        }
    }

    [Test]
    public void LockToFollowSelectsConfiguredCauseOnFinalExit()
    {
        CameraLockArea area = CreateLock("Area", 1);
        try
        {
            controller.EnterLockArea(area);
            controller.ExitLockArea(area);

            Assert.That(controller.CurrentLockArea, Is.Null);
            Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.LockToFollow));
            Assert.That(controller.TransitionSourceLockArea, Is.SameAs(area));
            Assert.That(controller.TransitionDestinationLockArea, Is.Null);
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [Test]
    public void SceneStartApplicationIsImmediateAndBypassesLiveTransition()
    {
        CameraLockArea area = CreateLock("Area", 1);
        try
        {
            // Restore the scene-start lock timer SetUp cleared, then simulate the overlap
            // refresh that runs while still inside the scene-start window.
            controller.ResetStartTimer();
            controller.EnterLockArea(area);

            Assert.That(controller.LastApplicationWasImmediate, Is.True);
            Assert.That(controller.IsTransitioning, Is.False);
            Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.None));
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [Test]
    public void DuplicateLockEntryDoesNotRestartTransition()
    {
        CameraLockArea area = CreateLock("Area", 1);
        try
        {
            controller.EnterLockArea(area);
            SetField(controller, "transitionElapsed", 0.2f);

            controller.EnterLockArea(area);

            Assert.That(GetField(controller, "transitionElapsed"), Is.EqualTo(0.2f));
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [Test]
    public void NonActiveLockRemovalDoesNotRestartTransition()
    {
        CameraLockArea active = CreateLock("Active", 5);
        CameraLockArea inactive = CreateLock("Inactive", 1);
        try
        {
            controller.EnterLockArea(active);
            controller.EnterLockArea(inactive);
            SetField(controller, "transitionElapsed", 0.2f);
            CameraTransitionCause causeBefore = controller.CurrentTransitionCause;

            controller.ExitLockArea(inactive);

            Assert.That(controller.CurrentLockArea, Is.SameAs(active));
            Assert.That(controller.CurrentTransitionCause, Is.EqualTo(causeBefore));
            Assert.That(GetField(controller, "transitionElapsed"), Is.EqualTo(0.2f));
        }
        finally
        {
            DestroyLocks(active, inactive);
        }
    }

    [Test]
    public void AreaOverrideWinsOverSharedDefault()
    {
        CameraLockArea area = CreateLock("Area", 1);
        try
        {
            CameraTransitionSettings overrideSettings = CameraTransitionSettings.Live(0.42f, 0.9f, true);
            SetField(area, "useTransitionOverride", true);
            SetField(area, "entryTransitionOverride", overrideSettings);

            controller.EnterLockArea(area);

            Assert.That(controller.CurrentDampTimeX, Is.EqualTo(0.42f).Within(0.0001f));
            Assert.That(controller.CurrentDampTimeY, Is.EqualTo(0.42f).Within(0.0001f));
            Assert.That(controller.TransitionDuration, Is.EqualTo(0.9f).Within(0.0001f));
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [Test]
    public void MissingOverrideUsesSharedDefault()
    {
        CameraLockArea area = CreateLock("Area", 1);
        try
        {
            CameraTransitionSettings expected = (CameraTransitionSettings)GetField(controller, "followToLockTransition");

            controller.EnterLockArea(area);

            Assert.That(controller.CurrentDampTimeX, Is.EqualTo(expected.dampTimeX).Within(0.0001f));
            Assert.That(controller.TransitionDuration, Is.EqualTo(expected.blendDuration).Within(0.0001f));
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [Test]
    public void LockEntryUnderFreezeDoesNotStartLiveTransition()
    {
        controller.FreezeInPlace();
        CameraLockArea area = CreateLock("Area", 1);
        try
        {
            controller.EnterLockArea(area);

            Assert.That(controller.CurrentLockArea, Is.SameAs(area));
            Assert.That(controller.IsTransitioning, Is.False);
            Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.None));
        }
        finally
        {
            controller.StopFreeze();
            DestroyLocks(area);
        }
    }

    [Test]
    public void FinalFreezeReleaseTransitionsTowardCurrentUnderlyingLock()
    {
        CameraLockArea first = CreateLock("First", 1);
        CameraLockArea second = CreateLock("Second", 2);
        try
        {
            controller.EnterLockArea(first);
            controller.FreezeInPlace();
            controller.ExitLockArea(first);
            controller.EnterLockArea(second);

            Assert.That(controller.CurrentLockArea, Is.SameAs(second));

            controller.StopFreeze();

            Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.OverrideReleased));
            Assert.That(controller.TransitionDestinationLockArea, Is.SameAs(second));
            Assert.That(controller.TransitionSourceLockArea, Is.SameAs(second));
        }
        finally
        {
            DestroyLocks(first, second);
        }
    }

    [Test]
    public void TransitionClearsWhenBlendCompletes()
    {
        CameraLockArea area = CreateLock("Area", 1);
        try
        {
            controller.EnterLockArea(area);
            Assert.That(controller.IsTransitioning, Is.True);

            SetField(controller, "transitionElapsed", 999f);
            Invoke(controller, "UpdateDampTimes");

            Assert.That(controller.IsTransitioning, Is.False);
            Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.None));
            Assert.That(controller.TransitionSourceLockArea, Is.Null);
            Assert.That(controller.TransitionDestinationLockArea, Is.Null);
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [Test]
    public void SceneRebindClearsStaleTransitionReferences()
    {
        CameraLockArea area = CreateLock("Area", 1);
        try
        {
            controller.EnterLockArea(area);
            Assert.That(controller.IsTransitioning, Is.True);

            controller.SceneInit();

            Assert.That(controller.IsTransitioning, Is.False);
            Assert.That(controller.CurrentTransitionCause, Is.EqualTo(CameraTransitionCause.None));
            Assert.That(controller.TransitionSourceLockArea, Is.Null);
            Assert.That(controller.TransitionDestinationLockArea, Is.Null);
            Assert.That(controller.LastApplicationWasImmediate, Is.True);
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [Test]
    public void SnapToTargetEndsAnyInProgressTransition()
    {
        CameraLockArea area = CreateLock("Area", 1);
        try
        {
            controller.EnterLockArea(area);
            Assert.That(controller.IsTransitioning, Is.True);

            controller.SnapToTarget();

            Assert.That(controller.IsTransitioning, Is.False);
            Assert.That(controller.LastApplicationWasImmediate, Is.True);
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [Test]
    public void RegisteredLocksAreReportedInSelectionOrder()
    {
        CameraLockArea low = CreateLock("Low", 1);
        CameraLockArea high = CreateLock("High", 5);
        try
        {
            controller.EnterLockArea(low);
            controller.EnterLockArea(high);

            var registered = controller.GetRegisteredLockAreasSorted();

            Assert.That(registered.Count, Is.EqualTo(2));
            Assert.That(registered[0], Is.SameAs(high));
            Assert.That(registered[1], Is.SameAs(low));
        }
        finally
        {
            DestroyLocks(low, high);
        }
    }

    [Test]
    public void LegalRegionCollapsesForLockSmallerThanViewport()
    {
        CameraLockArea area = CreateLock("Tiny", 1, size: new Vector2(0.5f, 0.5f));
        try
        {
            controller.EnterLockArea(area);

            CameraLegalRegion region = controller.GetLegalRegion();

            Assert.That(region.XConstrained, Is.True);
            Assert.That(region.YConstrained, Is.True);
            Assert.That(region.MinX, Is.EqualTo(region.MaxX).Within(0.0001f));
            Assert.That(region.MinY, Is.EqualTo(region.MaxY).Within(0.0001f));
        }
        finally
        {
            DestroyLocks(area);
        }
    }

    [TestCase(float.NaN, 0f, 0f, false)]
    [TestCase(0f, float.PositiveInfinity, 0f, false)]
    [TestCase(-0.1f, 0f, 0f, true)]
    [TestCase(0.1f, 0.1f, 0.3f, true)]
    public void TransitionSettingsValidityMatchesExpectation(float dampX, float dampY, float blend, bool expectedFinite)
    {
        CameraTransitionSettings settings = new CameraTransitionSettings
        {
            dampTimeX = dampX,
            dampTimeY = dampY,
            blendDuration = blend
        };

        Assert.That(settings.IsFinite(), Is.EqualTo(expectedFinite));
    }

    [Test]
    public void NegativeBlendDurationFailsNonNegativeCheck()
    {
        CameraTransitionSettings settings = CameraTransitionSettings.Live(0.1f, -0.1f, false);

        Assert.That(settings.IsNonNegative(), Is.False);
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
}
