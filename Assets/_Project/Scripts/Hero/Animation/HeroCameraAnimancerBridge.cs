using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroCameraAnimancerBridge : MonoBehaviour
{
    public void RequestSmallShake()
    {
        CameraEventService.RequestShake(CameraShakeIntensity.Small, transform.position, 1f, this);
    }

    public void RequestMediumShake()
    {
        CameraEventService.RequestShake(CameraShakeIntensity.Medium, transform.position, 1f, this);
    }

    public void RequestIntenseShake()
    {
        CameraEventService.RequestShake(CameraShakeIntensity.Intense, transform.position, 1f, this);
    }

    public void RequestHardFreeze(float duration)
    {
        CameraEventService.RequestFreeze(CameraFreezeKind.Hard, duration, this);
    }

    public void RequestSoftFreeze(float duration)
    {
        CameraEventService.RequestFreeze(CameraFreezeKind.Soft, duration, this);
    }

    public void ReleaseFreeze()
    {
        CameraEventService.RequestFreeze(CameraFreezeKind.Release, -1f, this);
    }
}
