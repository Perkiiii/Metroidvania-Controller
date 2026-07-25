using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CameraPhaseOneValidatorTests
{
    [Test]
    public void LockReportsTriggerAxisAndTerrainLayerProblems()
    {
        GameObject go = new GameObject("Invalid Lock");
        try
        {
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            CameraLockArea area = go.AddComponent<CameraLockArea>();
            collider.isTrigger = false;
            SetField(area, "lockX", false);
            SetField(area, "lockY", false);
            LogAssert.ignoreFailingMessages = true;

            int issues = CameraPhaseOneValidator.ValidateLock(area, 1 << go.layer);

            Assert.That(issues, Is.EqualTo(3));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ExplicitLookOverridesRequireMatchingPreventionFlags()
    {
        GameObject go = new GameObject("Invalid Look");
        try
        {
            go.AddComponent<BoxCollider2D>().isTrigger = true;
            CameraLockArea area = go.AddComponent<CameraLockArea>();
            SetField(area, "overrideLookYMin", true);
            SetField(area, "overrideLookYMax", true);
            LogAssert.ignoreFailingMessages = true;

            int issues = CameraPhaseOneValidator.ValidateLock(area, 0);

            Assert.That(issues, Is.EqualTo(2));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void InvalidLookOrderingIsDetected()
    {
        GameObject go = new GameObject("Invalid Order");
        try
        {
            go.AddComponent<BoxCollider2D>().isTrigger = true;
            CameraLockArea area = go.AddComponent<CameraLockArea>();
            SetField(area, "preventLookDown", true);
            SetField(area, "overrideLookYMin", true);
            SetField(area, "lookYMin", 2f);
            SetField(area, "preventLookUp", true);
            SetField(area, "overrideLookYMax", true);
            SetField(area, "lookYMax", 1f);
            LogAssert.ignoreFailingMessages = true;

            Assert.That(CameraPhaseOneValidator.ValidateLock(area, 0), Is.EqualTo(1));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ValidBoundsPasses()
    {
        GameObject go = new GameObject("Valid Bounds");
        try
        {
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            go.AddComponent<BoxCollider2D>().isTrigger = true;
            CameraBoundsVolume volume = go.AddComponent<CameraBoundsVolume>();

            Assert.That(CameraPhaseOneValidator.ValidateBounds(volume, 1 << LayerMask.NameToLayer("Terrain")), Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }
}
