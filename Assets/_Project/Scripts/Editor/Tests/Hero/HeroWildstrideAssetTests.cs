using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class HeroWildstrideAssetTests
{
    [Test]
    public void ProductionInputUsesDashOnlyForDashAndWildstride()
    {
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/_Project/Input/InputSystem_Actions.inputactions");

        Assert.That(actions, Is.Not.Null);
        Assert.That(actions.FindAction("Player/Dash", false), Is.Not.Null);
        Assert.That(actions.FindAction("Player/Sprint", false), Is.Null);
    }

    [Test]
    public void ProductionWildstrideTuningSitsBetweenRunAndDash()
    {
        HeroConfig heroConfig = AssetDatabase.LoadAssetAtPath<HeroConfig>(
            "Assets/_Project/ScriptableObjects/Hero/HeroConfig.asset");
        HeroAbilityConfig abilityConfig = AssetDatabase.LoadAssetAtPath<HeroAbilityConfig>(
            "Assets/_Project/ScriptableObjects/Hero/HeroAbilityConfig.asset");

        Assert.That(abilityConfig.sprintSpeed, Is.GreaterThan(heroConfig.runSpeed));
        Assert.That(abilityConfig.sprintSpeed, Is.LessThan(abilityConfig.dashSpeed));
        Assert.That(abilityConfig.sprintJumpSpeed, Is.GreaterThan(heroConfig.runSpeed));
        Assert.That(abilityConfig.sprintLedgeJumpBufferTime, Is.EqualTo(0.08f).Within(0.0001f));
        Assert.That(abilityConfig.sprintLedgeJumpBufferTime, Is.LessThanOrEqualTo(heroConfig.coyoteTime));
    }

    [Test]
    public void ProductionLocomotionClipsSeparateWalkFromExplicitWildstride()
    {
        HeroAnimationLibrary library = AssetDatabase.LoadAssetAtPath<HeroAnimationLibrary>(
            "Assets/_Project/ScriptableObjects/Hero/HeroAnimationLibrary.asset");

        Assert.That(library, Is.Not.Null);
        Assert.That(library.walk, Is.Not.Null);
        Assert.That(library.walk.name, Is.EqualTo("HeroWalk"));
        Assert.That(library.sprint, Is.Not.Null);
        Assert.That(library.sprint.name, Is.EqualTo("HeroRun"));
        Assert.That(library.sprint, Is.Not.EqualTo(library.walk));
    }

    [Test]
    public void ProductionSprintUnlockRemainsLocked()
    {
        string yaml = System.IO.File.ReadAllText(
            "Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset");
        Assert.That(yaml, Does.Contain("sprintUnlocked: 0"));
    }

    [Test]
    public void HeroPrefabHasNoObsoleteSprintInputReference()
    {
        string yaml = System.IO.File.ReadAllText("Assets/_Project/Prefabs/Hero.prefab");
        Assert.That(yaml, Does.Not.Contain("sprintAction:"));

        GameObject root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Hero.prefab");
        try
        {
            HeroInputReader reader = root.GetComponent<HeroInputReader>();
            SerializedObject serializedReader = new SerializedObject(reader);
            Object dashReference = serializedReader.FindProperty("dashAction").objectReferenceValue;
            Assert.That(dashReference, Is.Not.Null);
            Assert.That(dashReference.name, Is.EqualTo("Player/Dash"));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
