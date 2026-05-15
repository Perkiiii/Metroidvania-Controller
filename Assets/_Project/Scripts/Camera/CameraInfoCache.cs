using UnityEngine;

public static class CameraInfoCache
{
    public static int FrameStamp { get; private set; } = -1;
    public static Camera MainCamera { get; private set; }
    public static Vector3 Position { get; private set; }
    public static float Aspect { get; private set; }
    public static float HalfWidth { get; private set; }
    public static float HalfHeight { get; private set; }
    public static Rect WorldRect => new Rect(Position.x - HalfWidth, Position.y - HalfHeight, HalfWidth * 2f, HalfHeight * 2f);

    public static void UpdateCache(Camera camera, bool force = false)
    {
        if (camera == null)
        {
            return;
        }

        if (!force && FrameStamp == Time.frameCount)
        {
            return;
        }

        FrameStamp = Time.frameCount;
        MainCamera = camera;
        Position = camera.transform.position;
        Aspect = camera.aspect;

        float height = camera.orthographic
            ? camera.orthographicSize * 2f
            : 2f * Mathf.Abs(Position.z) * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);

        HalfHeight = height * 0.5f;
        HalfWidth = HalfHeight * Aspect;
    }
}
