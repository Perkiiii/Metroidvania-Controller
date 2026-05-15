using UnityEngine;

public interface ICameraShakeService
{
    void Shake(CameraShakeProfile profile, Vector2 worldPosition, float intensityMultiplier = 1f);
    void Shake(CameraShakeIntensity preset, Vector2 worldPosition, float intensityMultiplier = 1f);
    void Cancel(object source);
}
