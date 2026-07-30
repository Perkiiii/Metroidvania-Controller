using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class HeroLedgeClimbAssetTests
{
    private const string ClipPath = "Assets/_Project/Animations/Hero Animations/HeroLedge.anim";
    private const string LibraryPath = "Assets/_Project/ScriptableObjects/Hero/HeroAnimationLibrary.asset";
    private const string ConfigPath = "Assets/_Project/ScriptableObjects/Hero/HeroConfig.asset";

    [Test]
    public void HeroLedgeClipIsPresentationOnlyAndNonLooping()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);

        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.isLooping, Is.False);
        Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty);
        Assert.That(AnimationUtility.GetCurveBindings(clip), Is.Empty, "The ledge clip must not contain transform or root-motion curves.");
        Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(clip).Length, Is.EqualTo(1), "The clip should retain its sprite presentation curve.");
    }

    [Test]
    public void HeroAnimationLibraryAssignsExistingLedgeClip()
    {
        HeroAnimationLibrary library = AssetDatabase.LoadAssetAtPath<HeroAnimationLibrary>(LibraryPath);
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);

        Assert.That(library, Is.Not.Null);
        Assert.That(library.ledgeClimb, Is.EqualTo(clip));
    }

    [Test]
    public void ProductionLedgeTuningUsesDedicatedTerrainMaskAndCodeTiming()
    {
        HeroConfig config = AssetDatabase.LoadAssetAtPath<HeroConfig>(ConfigPath);

        Assert.That(config, Is.Not.Null);
        Assert.That(config.ledgeSurfaceLayers.value, Is.EqualTo(1 << LayerMask.NameToLayer("Terrain")));
        Assert.That(config.ledgeCatchDuration, Is.EqualTo(0.08f));
        Assert.That(config.ledgePullUpDuration, Is.EqualTo(0.28f));
        Assert.That(config.ledgeSettleDuration, Is.EqualTo(0.05f));
    }
}
