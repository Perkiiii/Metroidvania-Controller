using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class CameraGizmoUtility
{
    private const float DefaultFieldOfView = 24f;
    private const float DefaultCameraZ = -38.1f;
    private const float DefaultAspect = 16f / 9f;

    public static void GetApproximateFrustumHalfExtents(out float halfWidth, out float halfHeight, out bool isLiveValue)
    {
        if (Application.isPlaying && CameraInfoCache.HalfWidth > 0f && CameraInfoCache.HalfHeight > 0f)
        {
            halfWidth = CameraInfoCache.HalfWidth;
            halfHeight = CameraInfoCache.HalfHeight;
            isLiveValue = true;
            return;
        }

        float fov = DefaultFieldOfView;
        float z = DefaultCameraZ;
        float aspect = DefaultAspect;
        isLiveValue = false;

#if UNITY_EDITOR
        CameraConfig config = FindAnyCameraConfig();
        if (config != null)
        {
            fov = config.fieldOfView;
            z = config.cameraZ;
        }
#endif

        halfHeight = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * Mathf.Abs(z);
        halfWidth = halfHeight * aspect;
    }

    public static void GetInsetInterval(float min, float max, float halfExtent, out float insetMin, out float insetMax, out bool collapsed)
    {
        insetMin = min + halfExtent;
        insetMax = max - halfExtent;
        collapsed = insetMin > insetMax;
        if (collapsed)
        {
            float center = (min + max) * 0.5f;
            insetMin = insetMax = center;
        }
    }

#if UNITY_EDITOR
    private static CameraConfig FindAnyCameraConfig()
    {
        string[] guids = AssetDatabase.FindAssets("t:CameraConfig");
        if (guids == null || guids.Length == 0)
        {
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<CameraConfig>(path);
    }
#endif
}
