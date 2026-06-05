using UnityEngine;

[CreateAssetMenu(menuName = "Project/Scene Transitions/Fade Profile", fileName = "FadeProfile")]
public sealed class FadeProfile : ScriptableObject
{
    [SerializeField] private float fadeOutDuration = 0.25f;
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float holdAtBlackDuration;
    [SerializeField] private AnimationCurve fadeOutCurve;
    [SerializeField] private AnimationCurve fadeInCurve;

    public float FadeOutDuration => fadeOutDuration;
    public float FadeInDuration => fadeInDuration;
    public float HoldAtBlackDuration => holdAtBlackDuration;
    public AnimationCurve FadeOutCurve => fadeOutCurve;
    public AnimationCurve FadeInCurve => fadeInCurve;
}
