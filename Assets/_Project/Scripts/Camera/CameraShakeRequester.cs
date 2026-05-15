using UnityEngine;

public static class CameraShakeRequester
{
    public static void ShakeHit()
    {
        CameraEventService.RequestShake(CameraShakeIntensity.Small, Vector2.zero);
    }

    public static void ShakeHeavy()
    {
        CameraEventService.RequestShake(CameraShakeIntensity.Medium, Vector2.zero);
    }

    public static void FallRumble()
    {
        CameraEventService.RequestShake(CameraShakeIntensity.FallRumble, Vector2.zero);
    }

    public static void ShakeStop()
    {
        CameraEventService.RequestShakeCancel();
    }
}
