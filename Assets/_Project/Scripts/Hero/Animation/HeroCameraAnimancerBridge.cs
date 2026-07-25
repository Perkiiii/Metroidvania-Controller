using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroCameraAnimancerBridge : MonoBehaviour
{
    private CameraRequestHandle freezeHandle;

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
        freezeHandle.Release();
        freezeHandle = CameraEventService.AcquireFreeze(CameraFreezeKind.Hard, duration, this);
    }

    public void RequestSoftFreeze(float duration)
    {
        freezeHandle.Release();
        freezeHandle = CameraEventService.AcquireFreeze(CameraFreezeKind.Soft, duration, this);
    }

    public void ReleaseFreeze()
    {
        freezeHandle.Release();
        freezeHandle = default;
    }

    private void OnDisable()
    {
        ReleaseFreeze();
    }
}
