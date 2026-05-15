using UnityEngine;

public enum CameraShakeIntensity
{
    Small,
    Medium,
    Intense,
    FallRumble
}

[CreateAssetMenu(menuName = "World/Camera Shake Profile", fileName = "CameraShakeProfile")]
public sealed class CameraShakeProfile : ScriptableObject
{
    [SerializeField] private CameraShakeIntensity intensity = CameraShakeIntensity.Small;
    [SerializeField] private float duration = 0.15f;
    [SerializeField] private float amplitude = 0.12f;
    [SerializeField] private float frequency = 25f;
    [SerializeField] private bool infinite;
    [SerializeField] private bool useUnscaledTime;

    public CameraShakeIntensity Intensity => intensity;
    public float Duration => duration;
    public float Amplitude => amplitude;
    public float Frequency => frequency;
    public bool Infinite => infinite;
    public bool UseUnscaledTime => useUnscaledTime;
}
