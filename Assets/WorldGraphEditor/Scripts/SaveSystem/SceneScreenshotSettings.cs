#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor
{
    [Serializable]
    public struct SceneScreenshotSettings
    {
        public float Distance;
        public bool IsOrthographic;
        public ScreenshotResolutionType Resolution;
        public TextureImporterNPOTScale NpotScale;
        public Vector3 Center;
        public Quaternion Rotation;
        public Vector2 Size;
        public string SceneGuid;

        public static SceneScreenshotSettings Default => new()
        {
            Distance = 20,
            Size = new Vector2(20, 15),
            Rotation = Quaternion.Euler(270, 0, 0),
            Resolution = 0,
            IsOrthographic = true
        };
    }
}
#endif