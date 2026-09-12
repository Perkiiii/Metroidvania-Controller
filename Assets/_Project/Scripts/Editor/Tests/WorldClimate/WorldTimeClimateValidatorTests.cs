using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class WorldTimeClimateValidatorTests
{
    [Test]
    public void MissingContextIsReportedForGameplayScene()
    {
        List<string> issues = WorldTimeClimateValidator.GetContextIssues(
            "SampleScene",
            "Assets/_Project/Scenes/SampleScene.unity",
            false,
            new List<WorldTimeClimateValidator.ContextInfo>(),
            new HashSet<string> { "region_underbrew" });

        Assert.That(issues, Has.Count.EqualTo(1));
        StringAssert.Contains("missing its required single RoomClimateContext", issues[0]);
    }

    [Test]
    public void DuplicateContextsAreReportedWithObjectPaths()
    {
        List<string> issues = WorldTimeClimateValidator.GetContextIssues(
            "SampleScene",
            "Assets/_Project/Scenes/SampleScene.unity",
            false,
            new List<WorldTimeClimateValidator.ContextInfo>
            {
                new WorldTimeClimateValidator.ContextInfo("RoomClimateContext", "region_underbrew"),
                new WorldTimeClimateValidator.ContextInfo("RoomClimateContext (1)", "region_underbrew")
            },
            new HashSet<string> { "region_underbrew" });

        Assert.That(issues, Has.Count.EqualTo(1));
        StringAssert.Contains("duplicate RoomClimateContext", issues[0]);
        StringAssert.Contains("RoomClimateContext (1)", issues[0]);
    }

    [Test]
    public void BlankRegionIdIsReported()
    {
        List<string> issues = WorldTimeClimateValidator.GetContextIssues(
            "SampleScene",
            "Assets/_Project/Scenes/SampleScene.unity",
            false,
            new List<WorldTimeClimateValidator.ContextInfo>
            {
                new WorldTimeClimateValidator.ContextInfo("RoomClimateContext", " ")
            },
            new HashSet<string> { "region_underbrew" });

        Assert.That(issues, Has.Count.EqualTo(1));
        StringAssert.Contains("blank regionId", issues[0]);
    }

    [Test]
    public void UnknownRegionIdIsReported()
    {
        List<string> issues = WorldTimeClimateValidator.GetContextIssues(
            "SampleScene",
            "Assets/_Project/Scenes/SampleScene.unity",
            false,
            new List<WorldTimeClimateValidator.ContextInfo>
            {
                new WorldTimeClimateValidator.ContextInfo("RoomClimateContext", "region_missing")
            },
            new HashSet<string> { "region_underbrew" });

        Assert.That(issues, Has.Count.EqualTo(1));
        StringAssert.Contains("unknown regionId 'region_missing'", issues[0]);
    }

    [Test]
    public void BootRejectsAnyContextButDoesNotApplyGameplayCountRules()
    {
        List<string> issues = WorldTimeClimateValidator.GetContextIssues(
            "Boot",
            "Assets/_Project/Scenes/Boot.unity",
            true,
            new List<WorldTimeClimateValidator.ContextInfo>
            {
                new WorldTimeClimateValidator.ContextInfo("RoomClimateContext", "region_underbrew")
            },
            new HashSet<string> { "region_underbrew" });

        Assert.That(issues, Has.Count.EqualTo(1));
        StringAssert.Contains("must contain zero RoomClimateContext", issues[0]);
    }

    [Test]
    public void SharedRegionIdsAreValidAcrossMultipleGameplayScenes()
    {
        HashSet<string> regions = new HashSet<string> { "region_underbrew" };
        Assert.That(
            WorldTimeClimateValidator.GetContextIssues(
                "SampleScene",
                "Assets/_Project/Scenes/SampleScene.unity",
                false,
                new[] { new WorldTimeClimateValidator.ContextInfo("RoomClimateContext", "region_underbrew") },
                regions),
            Is.Empty);
        Assert.That(
            WorldTimeClimateValidator.GetContextIssues(
                "SampleScene2",
                "Assets/_Project/Scenes/SampleScene2.unity",
                false,
                new[] { new WorldTimeClimateValidator.ContextInfo("RoomClimateContext", "region_underbrew") },
                regions),
            Is.Empty);
    }

    [Test]
    public void RoomClimateContextKeepsSerializedFieldsPrivateAndReadOnly()
    {
        GameObject go = new GameObject("RoomClimateContext");
        try
        {
            RoomClimateContext context = go.AddComponent<RoomClimateContext>();
            SerializedObject serialized = new SerializedObject(context);
            SerializedProperty region = serialized.FindProperty("regionId");
            SerializedProperty exposure = serialized.FindProperty("exposure");

            Assert.That(region, Is.Not.Null);
            Assert.That(exposure, Is.Not.Null);
            region.stringValue = "region_underbrew";
            exposure.enumValueIndex = (int)EnvironmentExposure.Outdoor;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(context.RegionId, Is.EqualTo("region_underbrew"));
            Assert.That(context.Exposure, Is.EqualTo(EnvironmentExposure.Outdoor));
            Assert.That(typeof(RoomClimateContext).GetProperty("RegionId").CanWrite, Is.False);
            Assert.That(typeof(RoomClimateContext).GetProperty("Exposure").CanWrite, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void AuthoredClimateCatalogBuildsLookupAgainstWorldTimeGraph()
    {
        ClimateRegionCatalog catalog = AssetDatabase.LoadAssetAtPath<ClimateRegionCatalog>(
            "Assets/_Project/ScriptableObjects/World/ClimateRegionCatalog.asset");
        WorldTimeState worldTimeState = AssetDatabase.LoadAssetAtPath<WorldTimeState>(
            "Assets/_Project/ScriptableObjects/World/WorldTimeState.asset");

        Assert.That(
            WorldTimeClimateValidator.TryBuildRegionLookup(
                catalog,
                worldTimeState,
                out HashSet<string> regionIds,
                out string error),
            Is.True,
            error);
        Assert.That(regionIds, Does.Contain("region_underbrew"));
    }

    [Test]
    public void EnabledProjectScenesPassTheClimateContextContract()
    {
        Assert.That(WorldTimeClimateValidator.ValidateEnabledBuildSettingsScenesForTests(), Is.Zero);
    }
}
