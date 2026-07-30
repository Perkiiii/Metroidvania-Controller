using UnityEngine;

[CreateAssetMenu(menuName = "Hero/Hero Animation Library", fileName = "HeroAnimationLibrary")]
public sealed class HeroAnimationLibrary : ScriptableObject
{
    [Header("Locomotion")]
    public AnimationClip idle;
    public AnimationClip walk;
    public AnimationClip run;
    public AnimationClip sprint;

    [Header("Air")]
    public AnimationClip jump;
    public AnimationClip fall;

    [Header("Traversal")]
    public AnimationClip dash;
    public AnimationClip wallSlide;
    public AnimationClip wallJump;
    public AnimationClip ledgeClimb;

    [Header("Future Combat")]
    public AnimationClip attackSide;
    public AnimationClip attackUp;
    public AnimationClip attackDown;

    [Header("Damage")]
    public AnimationClip hurt;
    public AnimationClip death;

    [Header("Bind")]
    public AnimationClip bind;
}
