using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Package A1 correction-pass coverage for Finding #10: Sandbox fixture presets must be
/// deterministic across repeated clicks rather than accumulating state from prior applications.
/// </summary>
public sealed class UISandboxControllerFixtureTests
{
    private GameObject controllerObject;
    private UISandboxController controller;

    [SetUp]
    public void SetUp()
    {
        controllerObject = new GameObject("UISandboxController Test");
        controller = controllerObject.AddComponent<UISandboxController>();
        InvokePrivate(controller, "Awake");
    }

    [TearDown]
    public void TearDown()
    {
        PlayerHealthState health = (PlayerHealthState)GetPrivate(controller, "sandboxHealthState");
        PlayerResourceState resource = (PlayerResourceState)GetPrivate(controller, "sandboxResourceState");
        if (health != null) Object.DestroyImmediate(health);
        if (resource != null) Object.DestroyImmediate(resource);
        Object.DestroyImmediate(controllerObject);
    }

    [Test]
    public void BonusHealthFixtureIsDeterministicAcrossRepeatedClicks()
    {
        controller.ApplyHealthFixture_Bonus();
        controller.ApplyHealthFixture_Bonus();
        controller.ApplyHealthFixture_Bonus();

        PlayerHealthState health = (PlayerHealthState)GetPrivate(controller, "sandboxHealthState");
        Assert.That(health.BonusHealth, Is.EqualTo(2), "Repeated clicks must not keep increasing bonus health.");
    }

    [Test]
    public void ResourceFullFixtureIsDeterministicAcrossRepeatedClicksFromAnyPriorState()
    {
        controller.ApplyResourceFixture_Partial();
        controller.ApplyResourceFixture_Full();
        controller.ApplyResourceFixture_Full();

        PlayerResourceState resource = (PlayerResourceState)GetPrivate(controller, "sandboxResourceState");
        Assert.That(resource.CurrentParts, Is.EqualTo(10));
    }

    [Test]
    public void ResourcePartialFixtureIsDeterministicAcrossRepeatedClicks()
    {
        controller.ApplyResourceFixture_Full();
        controller.ApplyResourceFixture_Partial();
        controller.ApplyResourceFixture_Partial();

        PlayerResourceState resource = (PlayerResourceState)GetPrivate(controller, "sandboxResourceState");
        Assert.That(resource.CurrentParts, Is.EqualTo(6));
    }

    [Test]
    public void ResourceEmptyFixtureIsDeterministicAcrossRepeatedClicks()
    {
        controller.ApplyResourceFixture_Full();
        controller.ApplyResourceFixture_Empty();
        controller.ApplyResourceFixture_Empty();

        PlayerResourceState resource = (PlayerResourceState)GetPrivate(controller, "sandboxResourceState");
        Assert.That(resource.CurrentParts, Is.EqualTo(0));
    }

    private static object GetPrivate(object target, string name)
    {
        FieldInfo fi = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(fi, Is.Not.Null, "Field not found: " + name);
        return fi.GetValue(target);
    }

    private static void InvokePrivate(object target, string name)
    {
        MethodInfo mi = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(mi, Is.Not.Null, "Method not found: " + name);
        mi.Invoke(target, null);
    }
}
