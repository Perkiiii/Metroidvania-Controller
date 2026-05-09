using UnityEngine;

[CreateAssetMenu(menuName = "Hero/Hero Animation Library", fileName = "HeroAnimationLibrary")]
public sealed class HeroAnimationLibrary : ScriptableObject
{
    [Header("Locomotion")]
    public AnimationClip idle;
    public AnimationClip walk;
    public AnimationClip run;

    [Header("Air")]
    public AnimationClip jump;
    public AnimationClip fall;

    [Header("Future Traversal")]
    public AnimationClip dash;
    public AnimationClip wallSlide;
    public AnimationClip wallJump;

    [Header("Future Combat")]
    public AnimationClip attackSide;
    public AnimationClip attackUp;
    public AnimationClip attackDown;
}
