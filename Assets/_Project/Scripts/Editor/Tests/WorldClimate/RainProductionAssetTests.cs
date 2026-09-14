#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Guards the authored three-depth rain composition and project-owned dependencies.</summary>
public sealed class RainProductionAssetTests
{
    private const string ProjectRoot = "Assets/_Project/";
    private const string ThirdPartyRoot = "Assets/ThirdParty/HappyHarvest/";
    private const string SourceSplashPath =
        "Assets/ThirdParty/HappyHarvest/EnvironmentReference/HappyHarvest/VFX/Rain/RainSplashFlipbook.png";
    private const string ProjectSplashPath = "Assets/_Project/Art/Weather/Rain/RainSplashFlipbook.png";
    private const string SourceAmbiencePath =
        "Assets/ThirdParty/HappyHarvest/EnvironmentReference/HappyHarvest/Audio/Ambience/Rain.wav";
    private const string ProjectAmbiencePath = "Assets/_Project/Audio/Weather/Rain.wav";
    private const string RainMaterialPath = "Assets/_Project/Art/Weather/Rain/WorldRain.mat";
    private const string SplashMaterialPath = "Assets/_Project/Art/Weather/Rain/GroundSplash.mat";
    private const string BackRainPrefabPath = "Assets/_Project/Prefabs/Weather/Rain/BackRain.prefab";
    private const string MidRainPrefabPath = "Assets/_Project/Prefabs/Weather/Rain/WorldRain.prefab";
    private const string FrontRainPrefabPath = "Assets/_Project/Prefabs/Weather/Rain/FrontRain.prefab";
    private const string SplashPrefabPath = "Assets/_Project/Prefabs/Weather/Rain/GroundSplash.prefab";
    private const string PresentationPrefabPath =
        "Assets/_Project/Prefabs/Weather/Rain/RoomRainPresentation.prefab";
    private const string SampleScenePath = "Assets/_Project/Scenes/SampleScene.unity";

    [Test]
    public void CopiedRainAssetsHaveIndependentGuidsAndSixFrameSplashSheet()
    {
        AssertProjectAssetExists(ProjectSplashPath);
        AssertProjectAssetExists(ProjectAmbiencePath);

        string sourceSplashGuid = AssetDatabase.AssetPathToGUID(SourceSplashPath);
        string projectSplashGuid = AssetDatabase.AssetPathToGUID(ProjectSplashPath);
        string sourceAmbienceGuid = AssetDatabase.AssetPathToGUID(SourceAmbiencePath);
        string projectAmbienceGuid = AssetDatabase.AssetPathToGUID(ProjectAmbiencePath);

        Assert.That(sourceSplashGuid, Is.Not.Empty);
        Assert.That(projectSplashGuid, Is.Not.Empty);
        Assert.That(sourceAmbienceGuid, Is.Not.Empty);
        Assert.That(projectAmbienceGuid, Is.Not.Empty);
        Assert.That(projectSplashGuid, Is.Not.EqualTo(sourceSplashGuid));
        Assert.That(projectAmbienceGuid, Is.Not.EqualTo(sourceAmbienceGuid));

        TextureImporter importer = AssetImporter.GetAtPath(ProjectSplashPath) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(128f).Within(0.001f));
        Assert.That(AssetDatabase.LoadAllAssetsAtPath(ProjectSplashPath).OfType<Sprite>().Count(),
            Is.EqualTo(6));
        Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectAmbiencePath), Is.Not.Null);
    }

    [Test]
    public void RainPrefabsUseDistinctDepthTuningAndOnlyMidCollides()
    {
        Material rainMaterial = LoadRequired<Material>(RainMaterialPath);
        AssertUrpParticleMaterial(rainMaterial);
        Assert.That(rainMaterial.GetTexture("_BaseMap"), Is.Null);

        ParticleSystem back = LoadRequired<GameObject>(BackRainPrefabPath).GetComponent<ParticleSystem>();
        ParticleSystem mid = LoadRequired<GameObject>(MidRainPrefabPath).GetComponent<ParticleSystem>();
        ParticleSystem front = LoadRequired<GameObject>(FrontRainPrefabPath).GetComponent<ParticleSystem>();
        Assert.That(back, Is.Not.Null);
        Assert.That(mid, Is.Not.Null);
        Assert.That(front, Is.Not.Null);

        AssertRainRenderer(back, rainMaterial, -5);
        AssertRainRenderer(mid, rainMaterial, 20);
        AssertRainRenderer(front, rainMaterial, 25);
        Assert.That(back.main.startSize.constantMax, Is.LessThan(mid.main.startSize.constantMax));
        Assert.That(front.main.startSize.constantMin, Is.GreaterThan(mid.main.startSize.constantMin));
        Assert.That(back.main.startColor.color.a, Is.LessThan(mid.main.startColor.color.a));
        Assert.That(front.main.startColor.color.a, Is.LessThan(mid.main.startColor.color.a));
        Assert.That(back.collision.enabled, Is.False);
        Assert.That(back.collision.sendCollisionMessages, Is.False);
        Assert.That(front.collision.enabled, Is.False);
        Assert.That(front.collision.sendCollisionMessages, Is.False);

        ParticleSystem.CollisionModule collision = mid.collision;
        Assert.That(collision.enabled, Is.True);
        Assert.That(collision.type, Is.EqualTo(ParticleSystemCollisionType.World));
        Assert.That(collision.mode, Is.EqualTo(ParticleSystemCollisionMode.Collision2D));
        Assert.That(collision.collidesWith.value, Is.EqualTo(1 << 7));
        Assert.That(collision.enableDynamicColliders, Is.False);
        Assert.That(collision.sendCollisionMessages, Is.True);
        Assert.That(collision.lifetimeLoss.constant, Is.EqualTo(1f));
        Assert.That(collision.quality, Is.EqualTo(ParticleSystemCollisionQuality.High));
    }

    [Test]
    public void SplashPrefabIsOneBoundedManuallyEmittedSixFrameSystem()
    {
        Material splashMaterial = LoadRequired<Material>(SplashMaterialPath);
        AssertUrpParticleMaterial(splashMaterial);
        Assert.That(splashMaterial.GetTexture("_BaseMap"), Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(splashMaterial.GetTexture("_BaseMap")),
            Is.EqualTo(ProjectSplashPath));

        GameObject splashPrefab = LoadRequired<GameObject>(SplashPrefabPath);
        ParticleSystem splash = splashPrefab.GetComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = splashPrefab.GetComponent<ParticleSystemRenderer>();
        Assert.That(splash, Is.Not.Null);
        Assert.That(renderer, Is.Not.Null);
        Assert.That(splash.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(splash.main.maxParticles, Is.EqualTo(24));
        Assert.That(splash.emission.enabled, Is.False);
        Assert.That(splash.shape.enabled, Is.False);
        Assert.That(splash.textureSheetAnimation.enabled, Is.True);
        Assert.That(splash.textureSheetAnimation.numTilesX, Is.EqualTo(3));
        Assert.That(splash.textureSheetAnimation.numTilesY, Is.EqualTo(2));
        Assert.That(renderer.sharedMaterial, Is.SameAs(splashMaterial));
        Assert.That(renderer.sortingOrder, Is.EqualTo(21));
    }

    [Test]
    public void PresentationPrefabOwnsThreeLayersOneImpactHandlerAndNoAudioSource()
    {
        GameObject prefab = LoadRequired<GameObject>(PresentationPrefabPath);
        RoomRainPresentation rain = prefab.GetComponent<RoomRainPresentation>();
        RoomWeatherPresentation weather = prefab.GetComponent<RoomWeatherPresentation>();
        RoomStormPresentation storm = prefab.GetComponent<RoomStormPresentation>();
        Assert.That(rain, Is.Not.Null);
        Assert.That(weather, Is.Not.Null);
        Assert.That(storm, Is.Not.Null);
        Assert.That(rain.VisualLayerCount, Is.EqualTo(3));
        Assert.That(rain.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(4));
        Assert.That(rain.GetComponentsInChildren<RainImpactSplashHandler>(true).Length, Is.EqualTo(1));
        Assert.That(rain.ImpactSplashHandler.gameObject, Is.SameAs(rain.MidRainLayer.gameObject));
        Assert.That(rain.ImpactSplashHandler.CollisionSource, Is.SameAs(rain.MidRainLayer));
        Assert.That(rain.ImpactSplashHandler.SplashSystem, Is.SameAs(rain.SplashSystem));
        Assert.That(rain.GetComponentsInChildren<AudioSource>(true), Is.Empty);

        CollectionAssert.AreEquivalent(
            new[] { "BackRain", "MidRain", "FrontRain", "GroundSplashPool" },
            rain.GetComponentsInChildren<ParticleSystem>(true)
                .Select(system => system.gameObject.name)
                .ToArray());
        Assert.That(rain.transform.Find("GroundSplash_A"), Is.Null);
        Assert.That(rain.transform.Find("GroundSplash_B"), Is.Null);
        Assert.That(rain.transform.Find("GroundSplash_C"), Is.Null);

        SerializedObject serialized = new SerializedObject(rain);
        Assert.That(serialized.FindProperty("weatherPresentation").objectReferenceValue,
            Is.SameAs(weather));
        Assert.That(serialized.FindProperty("rainAmbienceClip").objectReferenceValue, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(
            serialized.FindProperty("rainAmbienceClip").objectReferenceValue),
            Is.EqualTo(ProjectAmbiencePath));
        Assert.That(serialized.FindProperty("backDepthFromCamera").floatValue,
            Is.EqualTo(44f).Within(0.001f));
        Assert.That(serialized.FindProperty("midDepthFromCamera").floatValue,
            Is.EqualTo(37.8f).Within(0.001f));
        Assert.That(serialized.FindProperty("frontDepthFromCamera").floatValue,
            Is.EqualTo(29.5f).Within(0.001f));
        Assert.That(serialized.FindProperty("terrainCollisionLayers").intValue, Is.EqualTo(1 << 7));

        AssertNoReferenceToThirdParty(BackRainPrefabPath);
        AssertNoReferenceToThirdParty(MidRainPrefabPath);
        AssertNoReferenceToThirdParty(FrontRainPrefabPath);
        AssertNoReferenceToThirdParty(SplashPrefabPath);
        AssertNoReferenceToThirdParty(PresentationPrefabPath);
    }

    [Test]
    public void SampleSceneUsesUpdatedPrefabWithoutLegacySplashPositions()
    {
        AssertNoReferenceToThirdParty(SampleScenePath);
        Scene scene = SceneManager.GetSceneByPath(SampleScenePath);
        bool openedForTest = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);
            openedForTest = true;
        }

        try
        {
            RoomClimateContext[] contexts = FindInScene<RoomClimateContext>(scene);
            RoomWeatherPresentation[] weather = FindInScene<RoomWeatherPresentation>(scene);
            RoomRainPresentation[] rain = FindInScene<RoomRainPresentation>(scene);
            Assert.That(contexts.Length, Is.EqualTo(1));
            Assert.That(weather.Length, Is.EqualTo(1));
            Assert.That(rain.Length, Is.EqualTo(1));
            Assert.That(rain[0].VisualLayerCount, Is.EqualTo(3));
            Assert.That(rain[0].GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(4));
            Assert.That(rain[0].GetComponentsInChildren<RainImpactSplashHandler>(true).Length,
                Is.EqualTo(1));
            Assert.That(rain[0].transform.Find("GroundSplash_A"), Is.Null);
            Assert.That(rain[0].transform.Find("GroundSplash_B"), Is.Null);
            Assert.That(rain[0].transform.Find("GroundSplash_C"), Is.Null);
            Assert.That(rain[0].GetComponentsInChildren<AudioSource>(true), Is.Empty);

            SerializedObject serializedWeather = new SerializedObject(weather[0]);
            Assert.That(serializedWeather.FindProperty("weatherState").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedWeather.FindProperty("roomClimateContext").objectReferenceValue,
                Is.SameAs(contexts[0]));
        }
        finally
        {
            if (openedForTest && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void AssertRainRenderer(
        ParticleSystem rain,
        Material expectedMaterial,
        int expectedSortingOrder)
    {
        Assert.That(rain.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(rain.main.cullingMode, Is.EqualTo(ParticleSystemCullingMode.AlwaysSimulate));
        Assert.That(rain.velocityOverLifetime.enabled, Is.True);
        Assert.That(rain.velocityOverLifetime.space,
            Is.EqualTo(ParticleSystemSimulationSpace.World));
        ParticleSystemRenderer renderer = rain.GetComponent<ParticleSystemRenderer>();
        Assert.That(renderer, Is.Not.Null);
        Assert.That(renderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Stretch));
        Assert.That(renderer.sharedMaterial, Is.SameAs(expectedMaterial));
        Assert.That(renderer.sortingLayerName, Is.EqualTo("Default"));
        Assert.That(renderer.sortingOrder, Is.EqualTo(expectedSortingOrder));
    }

    private static T LoadRequired<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(asset, Is.Not.Null, $"Missing required production asset: {path}");
        Assert.That(path.StartsWith(ProjectRoot, StringComparison.Ordinal), Is.True);
        return asset;
    }

    private static void AssertProjectAssetExists(string path)
    {
        Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Not.Empty, $"Missing asset: {path}");
        Assert.That(path.StartsWith(ProjectRoot, StringComparison.Ordinal), Is.True);
    }

    private static void AssertUrpParticleMaterial(Material material)
    {
        Assert.That(material.shader, Is.Not.Null);
        StringAssert.Contains("Universal Render Pipeline/Particles", material.shader.name);
    }

    private static void AssertNoReferenceToThirdParty(string assetPath)
    {
        string[] dependencies = AssetDatabase.GetDependencies(assetPath, true);
        foreach (string dependency in dependencies)
        {
            Assert.That(
                dependency.StartsWith(ThirdPartyRoot, StringComparison.OrdinalIgnoreCase),
                Is.False,
                $"Production rain asset references bundled source content: {assetPath} -> {dependency}");
        }
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        List<T> matches = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            matches.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return matches.ToArray();
    }
}
#endif
