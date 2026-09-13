#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Guards the authored rain slice against accidental references back into the bundled reference
/// content or unmanaged scene audio. These checks intentionally inspect production assets rather
/// than recreating the authoring path used to make them.
/// </summary>
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
    private const string RainPrefabPath = "Assets/_Project/Prefabs/Weather/Rain/WorldRain.prefab";
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

        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(ProjectSplashPath);
        Assert.That(subAssets.OfType<Sprite>().Count(), Is.EqualTo(6));
        Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectAmbiencePath), Is.Not.Null);
    }

    [Test]
    public void RainMaterialsAndPrefabsUseOnlyProjectOwnedDependencies()
    {
        Material rainMaterial = LoadRequired<Material>(RainMaterialPath);
        Material splashMaterial = LoadRequired<Material>(SplashMaterialPath);
        AssertUrpParticleMaterial(rainMaterial);
        AssertUrpParticleMaterial(splashMaterial);
        Assert.That(rainMaterial.GetTexture("_BaseMap"), Is.Null);
        Assert.That(splashMaterial.GetTexture("_BaseMap"), Is.Not.Null);
        Assert.That(
            AssetDatabase.GetAssetPath(splashMaterial.GetTexture("_BaseMap")),
            Is.EqualTo(ProjectSplashPath));

        GameObject rainPrefab = LoadRequired<GameObject>(RainPrefabPath);
        ParticleSystem rain = rainPrefab.GetComponent<ParticleSystem>();
        ParticleSystemRenderer rainRenderer = rainPrefab.GetComponent<ParticleSystemRenderer>();
        Assert.That(rain, Is.Not.Null);
        Assert.That(rainRenderer, Is.Not.Null);
        Assert.That(rain.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(rainRenderer.sharedMaterial, Is.SameAs(rainMaterial));
        Assert.That(rainRenderer.sortingLayerName, Is.EqualTo("Default"));

        GameObject splashPrefab = LoadRequired<GameObject>(SplashPrefabPath);
        ParticleSystem splash = splashPrefab.GetComponent<ParticleSystem>();
        ParticleSystemRenderer splashRenderer = splashPrefab.GetComponent<ParticleSystemRenderer>();
        Assert.That(splash, Is.Not.Null);
        Assert.That(splashRenderer, Is.Not.Null);
        Assert.That(splash.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(splash.textureSheetAnimation.enabled, Is.True);
        Assert.That(splash.textureSheetAnimation.numTilesX, Is.EqualTo(3));
        Assert.That(splash.textureSheetAnimation.numTilesY, Is.EqualTo(2));
        Assert.That(splashRenderer.sharedMaterial, Is.SameAs(splashMaterial));

        GameObject presentationPrefab = LoadRequired<GameObject>(PresentationPrefabPath);
        RoomRainPresentation presentation = presentationPrefab.GetComponent<RoomRainPresentation>();
        Assert.That(presentation, Is.Not.Null);
        ParticleSystem[] authoredParticles = presentationPrefab.GetComponentsInChildren<ParticleSystem>(true);
        Assert.That(authoredParticles.Length, Is.EqualTo(4));
        Assert.That(presentation.SplashEmitterCount, Is.EqualTo(3));
        Assert.That(presentationPrefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);

        SerializedObject serializedPresentation = new SerializedObject(presentation);
        SerializedProperty ambience = serializedPresentation.FindProperty("rainAmbienceClip");
        Assert.That(ambience.objectReferenceValue, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(ambience.objectReferenceValue), Is.EqualTo(ProjectAmbiencePath));

        AssertNoReferenceToThirdParty(RainMaterialPath);
        AssertNoReferenceToThirdParty(SplashMaterialPath);
        AssertNoReferenceToThirdParty(RainPrefabPath);
        AssertNoReferenceToThirdParty(SplashPrefabPath);
        AssertNoReferenceToThirdParty(PresentationPrefabPath);
    }

    [Test]
    public void SampleSceneContainsOneRainLayerAndExactlyThreeTopSurfaceSplashes()
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
            RoomWeatherPresentation[] weatherPresentations = FindInScene<RoomWeatherPresentation>(scene);
            RoomRainPresentation[] rainPresentations = FindInScene<RoomRainPresentation>(scene);
            Assert.That(contexts.Length, Is.EqualTo(1));
            Assert.That(weatherPresentations.Length, Is.EqualTo(1));
            Assert.That(rainPresentations.Length, Is.EqualTo(1));

            RoomRainPresentation rain = rainPresentations[0];
            Assert.That(rain.SplashEmitterCount, Is.EqualTo(3));
            Assert.That(rain.GetComponentsInChildren<AudioSource>(true), Is.Empty);

            ParticleSystem[] particles = rain.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(particles.Length, Is.EqualTo(4));
            List<ParticleSystem> splashes = particles
                .Where(particle => particle != rain.RainLayer)
                .ToList();
            Assert.That(splashes.Count, Is.EqualTo(3));

            string[] expectedNames = { "GroundSplash_A", "GroundSplash_B", "GroundSplash_C" };
            CollectionAssert.AreEquivalent(
                expectedNames,
                splashes.Select(splash => splash.gameObject.name).ToArray());
            foreach (ParticleSystem splash in splashes)
            {
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(splash.transform.eulerAngles.x, 0f)), Is.LessThan(0.01f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(splash.transform.eulerAngles.z, 0f)), Is.LessThan(0.01f));
                Assert.That(splash.transform.position.z, Is.EqualTo(-0.05f).Within(0.001f));
            }

            SerializedObject serializedWeather = new SerializedObject(weatherPresentations[0]);
            Assert.That(serializedWeather.FindProperty("weatherState").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedWeather.FindProperty("roomClimateContext").objectReferenceValue, Is.SameAs(contexts[0]));

            SerializedObject serializedRain = new SerializedObject(rain);
            Assert.That(serializedRain.FindProperty("rainLayer").objectReferenceValue, Is.SameAs(rain.RainLayer));
            Assert.That(serializedRain.FindProperty("splashEmitters").arraySize, Is.EqualTo(3));
            Assert.That(serializedRain.FindProperty("rainAmbienceClip").objectReferenceValue, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(serializedRain.FindProperty("rainAmbienceClip").objectReferenceValue),
                Is.EqualTo(ProjectAmbiencePath));
        }
        finally
        {
            if (openedForTest && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
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
