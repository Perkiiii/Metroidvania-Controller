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
/// Guards the authored storm presentation against unmanaged audio/VFX dependencies and accidental
/// divergence from the existing room rain composition.
/// </summary>
public sealed class StormProductionAssetTests
{
    private const string ProjectRoot = "Assets/_Project/";
    private const string ThirdPartyRoot = "Assets/ThirdParty/HappyHarvest/";
    private const string SourceThunderPath =
        "Assets/ThirdParty/HappyHarvest/EnvironmentReference/HappyHarvest/Audio/Ambience/Thunder.wav";
    private const string ProjectThunderPath = "Assets/_Project/Audio/Weather/Thunder.wav";
    private const string FlashSpritePath = "Assets/_Project/Lights/white_fader.png";
    private const string FlashMaterialPath = "Assets/_Project/Shaders/SpriteFlash.mat";
    private const string PresentationPrefabPath =
        "Assets/_Project/Prefabs/Weather/Rain/RoomRainPresentation.prefab";
    private const string SampleScenePath = "Assets/_Project/Scenes/SampleScene.unity";

    [Test]
    public void ThunderCopyHasIndependentGuidAndLoadsAsAudioClip()
    {
        AssertProjectAssetExists(ProjectThunderPath);
        string sourceGuid = AssetDatabase.AssetPathToGUID(SourceThunderPath);
        string projectGuid = AssetDatabase.AssetPathToGUID(ProjectThunderPath);

        Assert.That(sourceGuid, Is.Not.Empty);
        Assert.That(projectGuid, Is.Not.Empty);
        Assert.That(projectGuid, Is.Not.EqualTo(sourceGuid));
        Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectThunderPath), Is.Not.Null);
        AssertNoReferenceToThirdParty(ProjectThunderPath);
    }

    [Test]
    public void StormExtendsRainPrefabWithHiddenProjectOwnedFlashAndAudio()
    {
        AssertNoReferenceToThirdParty(PresentationPrefabPath);
        GameObject prefab = LoadRequired<GameObject>(PresentationPrefabPath);
        RoomWeatherPresentation weather = prefab.GetComponent<RoomWeatherPresentation>();
        RoomRainPresentation rain = prefab.GetComponent<RoomRainPresentation>();
        RoomStormPresentation storm = prefab.GetComponent<RoomStormPresentation>();

        Assert.That(weather, Is.Not.Null);
        Assert.That(rain, Is.Not.Null);
        Assert.That(storm, Is.Not.Null);
        Assert.That(storm.FlashRenderer, Is.Not.Null);
        Assert.That(storm.FlashRenderer.gameObject.name, Is.EqualTo("LightningFlash"));
        Assert.That(storm.FlashRenderer.enabled, Is.False);
        Assert.That(storm.FlashRenderer.sprite, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(storm.FlashRenderer.sprite), Is.EqualTo(FlashSpritePath));
        Assert.That(storm.FlashRenderer.sharedMaterial, Is.Not.Null);
        Assert.That(
            AssetDatabase.GetAssetPath(storm.FlashRenderer.sharedMaterial),
            Is.EqualTo(FlashMaterialPath));
        Assert.That(storm.FlashRenderer.sortingLayerName, Is.EqualTo("Default"));
        Assert.That(storm.FlashRenderer.sortingOrder, Is.GreaterThan(rain.RainLayer.GetComponent<ParticleSystemRenderer>().sortingOrder));
        Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);

        SerializedObject serializedStorm = new SerializedObject(storm);
        Assert.That(serializedStorm.FindProperty("weatherPresentation").objectReferenceValue,
            Is.SameAs(weather));
        Assert.That(serializedStorm.FindProperty("flashRenderer").objectReferenceValue,
            Is.SameAs(storm.FlashRenderer));
        UnityEngine.Object thunder = serializedStorm.FindProperty("thunderClip").objectReferenceValue;
        Assert.That(thunder, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(thunder), Is.EqualTo(ProjectThunderPath));
    }

    [Test]
    public void SampleSceneUsesOneRainStormPresentationComposition()
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
            RoomWeatherPresentation[] weather = FindInScene<RoomWeatherPresentation>(scene);
            RoomRainPresentation[] rain = FindInScene<RoomRainPresentation>(scene);
            RoomStormPresentation[] storm = FindInScene<RoomStormPresentation>(scene);
            Assert.That(weather.Length, Is.EqualTo(1));
            Assert.That(rain.Length, Is.EqualTo(1));
            Assert.That(storm.Length, Is.EqualTo(1));
            Assert.That(storm[0].FlashRenderer, Is.Not.Null);
            Assert.That(storm[0].FlashRenderer.enabled, Is.False);

            SerializedObject serializedStorm = new SerializedObject(storm[0]);
            Assert.That(serializedStorm.FindProperty("weatherPresentation").objectReferenceValue,
                Is.SameAs(weather[0]));
            Assert.That(
                AssetDatabase.GetAssetPath(serializedStorm.FindProperty("thunderClip").objectReferenceValue),
                Is.EqualTo(ProjectThunderPath));
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

    private static void AssertNoReferenceToThirdParty(string assetPath)
    {
        string[] dependencies = AssetDatabase.GetDependencies(assetPath, true);
        foreach (string dependency in dependencies)
        {
            Assert.That(
                dependency.StartsWith(ThirdPartyRoot, StringComparison.OrdinalIgnoreCase),
                Is.False,
                $"Production storm asset references bundled source content: {assetPath} -> {dependency}");
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
