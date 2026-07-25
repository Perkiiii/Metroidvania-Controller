using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CameraPhaseOneValidator
{
    private const string CameraPrefabPath = "Assets/_Project/Prefabs/Managers/_GameCameras.prefab";
    private const string HeroConfigPath = "Assets/_Project/ScriptableObjects/Hero/HeroConfig.asset";

    [MenuItem("Tools/Project/Validate Camera Phase 1")]
    public static void ValidateCameraPhaseOne()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogWarning("[CameraPhaseOneValidator] Validation cancelled because a loaded scene has unsaved changes.");
                return;
            }
        }

        int issues = ValidatePersistentPrefab(out float halfWidth, out float halfHeight);
        HeroConfig heroConfig = AssetDatabase.LoadAssetAtPath<HeroConfig>(HeroConfigPath);
        LayerMask terrainLayers = heroConfig != null ? heroConfig.terrainLayers : (LayerMask)0;
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                issues += ValidateScene(scene, terrainLayers, halfWidth, halfHeight);
            }
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }

        if (issues == 0)
        {
            Debug.Log("[CameraPhaseOneValidator] Camera Phase 1 validation passed.");
        }
        else
        {
            Debug.LogWarning($"[CameraPhaseOneValidator] Found {issues} issue(s). See earlier logs.");
        }
    }

    internal static int ValidateScene(
        Scene scene,
        LayerMask terrainLayers,
        float halfWidth,
        float halfHeight)
    {
        int issues = 0;
        List<CameraBoundsVolume> boundsVolumes = new List<CameraBoundsVolume>();
        List<CameraLockArea> lockAreas = new List<CameraLockArea>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            boundsVolumes.AddRange(root.GetComponentsInChildren<CameraBoundsVolume>(true));
            lockAreas.AddRange(root.GetComponentsInChildren<CameraLockArea>(true));
        }

        for (int i = 0; i < boundsVolumes.Count; i++)
        {
            issues += ValidateBounds(boundsVolumes[i], terrainLayers);
        }

        for (int i = 0; i < lockAreas.Count; i++)
        {
            issues += ValidateLock(lockAreas[i], terrainLayers);
            issues += ValidateLegalRegion(lockAreas[i], boundsVolumes, halfWidth, halfHeight);
        }

        return issues;
    }

    internal static int ValidateLock(CameraLockArea area, LayerMask terrainLayers)
    {
        if (area == null)
        {
            return 0;
        }

        int issues = 0;
        BoxCollider2D collider = area.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            return Error(area, "is missing its required BoxCollider2D.");
        }

        if (!collider.isTrigger)
        {
            issues += Error(area, "activation collider is not a trigger.");
        }

        if (!area.LockX && !area.LockY)
        {
            issues += Error(area, "does not own either camera axis.");
        }

        if ((terrainLayers.value & (1 << area.gameObject.layer)) != 0)
        {
            issues += Error(area, $"uses layer '{LayerMask.LayerToName(area.gameObject.layer)}', which is included in HeroConfig.terrainLayers.");
        }

        if (area.OverrideLookYMax && !area.PreventLookUp)
        {
            issues += Error(area, "authors a look maximum override while Prevent Look Up is disabled.");
        }

        if (area.OverrideLookYMin && !area.PreventLookDown)
        {
            issues += Error(area, "authors a look minimum override while Prevent Look Down is disabled.");
        }

        if (area.HasLookYMin && area.HasLookYMax && area.LookYMin > area.LookYMax)
        {
            issues += Error(area, "has Look Y Min greater than Look Y Max.");
        }

        Rect rect = GetWorldRect(collider);
        if (rect.width <= 0f || rect.height <= 0f)
        {
            issues += Error(area, "has a non-positive framed region.");
        }

        return issues;
    }

    internal static int ValidateBounds(CameraBoundsVolume volume, LayerMask terrainLayers)
    {
        if (volume == null)
        {
            return 0;
        }

        int issues = 0;
        BoxCollider2D collider = volume.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            return Error(volume, "is missing its required BoxCollider2D.");
        }

        if (!collider.isTrigger)
        {
            issues += Error(volume, "activation collider is not a trigger.");
        }

        if ((terrainLayers.value & (1 << volume.gameObject.layer)) != 0)
        {
            issues += Error(volume, $"uses layer '{LayerMask.LayerToName(volume.gameObject.layer)}', which is included in HeroConfig.terrainLayers.");
        }

        Rect rect = GetWorldRect(collider);
        if (rect.width <= 0f || rect.height <= 0f)
        {
            issues += Error(volume, "has a non-positive room region.");
        }

        return issues;
    }

    private static int ValidateLegalRegion(
        CameraLockArea area,
        IReadOnlyList<CameraBoundsVolume> boundsVolumes,
        float halfWidth,
        float halfHeight)
    {
        if (area == null || boundsVolumes.Count == 0)
        {
            return 0;
        }

        Rect lockRect = GetWorldRect(area.GetComponent<BoxCollider2D>());
        CameraBoundsVolume containingRoom = null;
        Rect roomRect = default;
        for (int i = 0; i < boundsVolumes.Count; i++)
        {
            Rect candidate = GetWorldRect(boundsVolumes[i].GetComponent<BoxCollider2D>());
            if (candidate.Overlaps(lockRect, true))
            {
                containingRoom = boundsVolumes[i];
                roomRect = candidate;
                break;
            }
        }

        if (containingRoom == null)
        {
            return Error(area, "does not overlap any CameraBoundsVolume in the same scene.");
        }

        int issues = 0;
        if (area.LockX && !InsetAxesOverlap(roomRect.xMin, roomRect.xMax, lockRect.xMin, lockRect.xMax, halfWidth))
        {
            issues += Error(area, $"has no viewport-inset X intersection with room '{GetHierarchyPath(containingRoom.transform)}'.");
        }

        if (area.LockY && !InsetAxesOverlap(roomRect.yMin, roomRect.yMax, lockRect.yMin, lockRect.yMax, halfHeight))
        {
            issues += Error(area, $"has no viewport-inset Y intersection with room '{GetHierarchyPath(containingRoom.transform)}'.");
        }

        if ((area.LockX && lockRect.width < halfWidth * 2f)
            || (area.LockY && lockRect.height < halfHeight * 2f))
        {
            Debug.LogWarning(
                $"[CameraPhaseOneValidator] '{area.gameObject.scene.path}::{GetHierarchyPath(area.transform)}' "
                + "is smaller than the reference viewport on at least one owned axis; runtime will center that axis deterministically.",
                area);
        }

        return issues;
    }

    private static int ValidatePersistentPrefab(out float halfWidth, out float halfHeight)
    {
        halfWidth = 0f;
        halfHeight = 0f;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[CameraPhaseOneValidator] Missing persistent camera prefab at '{CameraPrefabPath}'.");
            return 1;
        }

        GameCameras cameras = prefab.GetComponent<GameCameras>();
        if (cameras == null)
        {
            Debug.LogError($"[CameraPhaseOneValidator] '{CameraPrefabPath}' is missing GameCameras.", prefab);
            return 1;
        }

        int issues = 0;
        if (cameras.Controller == null) issues += Error(cameras, "is missing CameraController.");
        if (cameras.Target == null) issues += Error(cameras, "is missing CameraTarget.");
        if (cameras.Fade == null) issues += Error(cameras, "is missing CameraFade.");
        if (cameras.ShakeCues == null) issues += Error(cameras, "is missing CameraShakeCueService.");
        if (cameras.HudCamera == null) issues += Error(cameras, "is missing HUDCamera.");

        Camera gameplayCamera = cameras.Controller != null
            ? cameras.Controller.GetComponent<Camera>()
            : null;
        if (gameplayCamera == null)
        {
            issues += Error(cameras, "CameraController is missing its Camera component.");
        }
        else
        {
            float distance = Mathf.Abs(gameplayCamera.transform.localPosition.z);
            halfHeight = Mathf.Tan(gameplayCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance;
            halfWidth = halfHeight * (16f / 9f);
        }

        return issues;
    }

    private static bool InsetAxesOverlap(
        float roomMin,
        float roomMax,
        float lockMin,
        float lockMax,
        float halfExtent)
    {
        GetInset(roomMin, roomMax, halfExtent, out float roomInsetMin, out float roomInsetMax);
        GetInset(lockMin, lockMax, halfExtent, out float lockInsetMin, out float lockInsetMax);
        return Mathf.Max(roomInsetMin, lockInsetMin) <= Mathf.Min(roomInsetMax, lockInsetMax);
    }

    private static void GetInset(float min, float max, float halfExtent, out float insetMin, out float insetMax)
    {
        insetMin = min + halfExtent;
        insetMax = max - halfExtent;
        if (insetMin <= insetMax)
        {
            return;
        }

        insetMin = insetMax = (min + max) * 0.5f;
    }

    private static Rect GetWorldRect(BoxCollider2D collider)
    {
        if (collider == null)
        {
            return default;
        }

        Vector3 center = collider.transform.TransformPoint(collider.offset);
        Vector3 scale = collider.transform.lossyScale;
        Vector2 size = new Vector2(
            Mathf.Abs(collider.size.x * scale.x),
            Mathf.Abs(collider.size.y * scale.y));
        return new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
    }

    private static int Error(Component context, string message)
    {
        Debug.LogError(
            $"[CameraPhaseOneValidator] '{context.gameObject.scene.path}::{GetHierarchyPath(context.transform)}' {message}",
            context);
        return 1;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = $"{transform.name}/{path}";
        }

        return path;
    }
}
