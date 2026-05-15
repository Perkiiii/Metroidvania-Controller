using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using MoreMountains.Feedbacks;
using System.IO;

public static class GameCamerasBuilder
{
    private const string CameraConfigPath = "Assets/_Project/ScriptableObjects/World/CameraConfig.asset";

    [MenuItem("Tools/Metroidvania/Build _GameCameras")]
    public static void Build()
    {
        CameraConfig cameraConfig = GetOrCreateCameraConfig();

        // ---------------------------------------------------------------
        // Root
        // ---------------------------------------------------------------
        GameObject root = GameObject.Find("_GameCameras");
        if (root == null)
        {
            root = new GameObject("_GameCameras");
            Undo.RegisterCreatedObjectUndo(root, "Create _GameCameras");
        }

        // ---------------------------------------------------------------
        // CameraParent — receives shake offset from MMCameraShaker
        // ---------------------------------------------------------------
        GameObject cameraParentGO = GetOrCreateChild(root, "CameraParent");
        MMCameraShaker shaker = GetOrAddComponent<MMCameraShaker>(cameraParentGO);

        // ---------------------------------------------------------------
        // MainCamera  (child of CameraParent)
        // HK uses perspective + tk2d at FOV 24 / z -38.1.
        // Without tk2d we keep the Unity camera perspective and clamp with frustum math.
        // ---------------------------------------------------------------
        GameObject mainCamGO = GetOrCreateChild(cameraParentGO, "MainCamera");
        mainCamGO.tag = "MainCamera";

        Camera mainCam = GetOrAddComponent<Camera>(mainCamGO);
        mainCam.orthographic  = false;
        mainCam.fieldOfView   = cameraConfig.fieldOfView;
        mainCam.clearFlags    = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = Color.black;
        mainCam.nearClipPlane = cameraConfig.nearClipPlane;
        mainCam.farClipPlane  = cameraConfig.farClipPlane;
        mainCam.depth         = 0;
        mainCam.cullingMask   = ~(1 << LayerMask.NameToLayer("UI"));

        Vector3 mainCamPos = mainCamGO.transform.localPosition;
        mainCamPos.z = cameraConfig.cameraZ;
        mainCamGO.transform.localPosition = mainCamPos;

        GetOrAddComponent<AudioListener>(mainCamGO);
        CameraController camCtrl = GetOrAddComponent<CameraController>(mainCamGO);

        // ---------------------------------------------------------------
        // HUDCamera  (child of root, depth above main)
        // ---------------------------------------------------------------
        GameObject hudCamGO = GetOrCreateChild(root, "HUDCamera");

        Vector3 hudCamPos = hudCamGO.transform.localPosition;
        hudCamPos.z = -38.1f;
        hudCamGO.transform.localPosition = hudCamPos;

        Camera hudCam = GetOrAddComponent<Camera>(hudCamGO);
        hudCam.orthographic     = true;
        hudCam.orthographicSize = 8.71f;  // HK HUD camera value
        hudCam.clearFlags       = CameraClearFlags.Depth;
        hudCam.nearClipPlane    = 0.3f;
        hudCam.farClipPlane     = 1000f;
        hudCam.depth            = 100;
        hudCam.cullingMask      = 1 << LayerMask.NameToLayer("UI");

        // ---------------------------------------------------------------
        // FadeCanvas  (child of root)
        // ---------------------------------------------------------------
        GameObject canvasGO = GetOrCreateChild(root, "FadeCanvas");

        Canvas canvas = GetOrAddComponent<Canvas>(canvasGO);
        canvas.renderMode    = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera   = hudCam;
        canvas.planeDistance = 1f;
        canvas.sortingOrder  = 999;

        CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(canvasGO);
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        CanvasGroup fadeGroup = GetOrAddComponent<CanvasGroup>(canvasGO);
        fadeGroup.alpha          = 0f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable   = false;

        // FadeImage — solid black, stretched to fill
        GameObject imageGO = GetOrCreateChild(canvasGO, "FadeImage");
        Image img = GetOrAddComponent<Image>(imageGO);
        img.color         = Color.black;
        img.raycastTarget = false;

        RectTransform imgRect = imageGO.GetComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.offsetMin = Vector2.zero;
        imgRect.offsetMax = Vector2.zero;

        // ---------------------------------------------------------------
        // CameraTarget  (child of root)
        // ---------------------------------------------------------------
        GameObject camTargetGO = GetOrCreateChild(root, "CameraTarget");
        CameraTarget camTarget = GetOrAddComponent<CameraTarget>(camTargetGO);

        // ---------------------------------------------------------------
        // CameraFade + camera routing on root
        // ---------------------------------------------------------------
        CameraFade cameraFade   = GetOrAddComponent<CameraFade>(root);
        CameraShakeCueService shakeCues = GetOrAddComponent<CameraShakeCueService>(root);
        GameCameras gameCameras = GetOrAddComponent<GameCameras>(root);

        // ---------------------------------------------------------------
        // Wire serialized references
        // ---------------------------------------------------------------
        WireGameCameras(gameCameras, camCtrl, camTarget, cameraFade, shakeCues, hudCam);
        WireCameraController(camCtrl, camTarget, cameraParentGO.transform);
        WireCameraFade(cameraFade, fadeGroup);
        WireCameraConfig(camCtrl, camTarget, cameraConfig);
        ApplyCameraTargetTuning(camTarget, cameraConfig);
        ApplyCameraControllerTuning(camCtrl, cameraConfig);

        EditorUtility.SetDirty(root);
        Debug.Log("[GameCamerasBuilder] Done. Save the scene then drag _GameCameras into Prefabs/Managers.");
    }

    // ------------------------------------------------------------------
    // Wiring
    // ------------------------------------------------------------------

    private static void WireGameCameras(GameCameras gc, CameraController ctrl,
        CameraTarget target, CameraFade fade, CameraShakeCueService shakeCues, Camera hudCam)
    {
        SerializedObject so = new SerializedObject(gc);
        so.FindProperty("cameraController").objectReferenceValue = ctrl;
        so.FindProperty("cameraTarget").objectReferenceValue     = target;
        so.FindProperty("cameraFade").objectReferenceValue       = fade;
        so.FindProperty("shakeCues").objectReferenceValue        = shakeCues;
        so.FindProperty("hudCamera").objectReferenceValue        = hudCam;
        so.ApplyModifiedProperties();
    }

    private static void WireCameraController(CameraController ctrl,
        CameraTarget target, Transform cameraParent)
    {
        SerializedObject so = new SerializedObject(ctrl);
        so.FindProperty("cameraTarget").objectReferenceValue = target;
        so.FindProperty("cameraParent").objectReferenceValue = cameraParent;
        so.ApplyModifiedProperties();
    }

    private static void WireCameraFade(CameraFade fade, CanvasGroup group)
    {
        SerializedObject so = new SerializedObject(fade);
        so.FindProperty("fadeGroup").objectReferenceValue = group;
        so.ApplyModifiedProperties();
    }

    private static void WireCameraConfig(CameraController ctrl, CameraTarget target,
        CameraConfig config)
    {
        SerializedObject ctrlSo = new SerializedObject(ctrl);
        ctrlSo.FindProperty("config").objectReferenceValue = config;
        ctrlSo.ApplyModifiedProperties();

        SerializedObject targetSo = new SerializedObject(target);
        targetSo.FindProperty("config").objectReferenceValue = config;
        targetSo.ApplyModifiedProperties();
    }

    // ------------------------------------------------------------------
    // Tuning values sourced from HK _GameCameras prefab
    // ------------------------------------------------------------------

    private static void ApplyCameraTargetTuning(CameraTarget t, CameraConfig config)
    {
        SerializedObject so = new SerializedObject(t);
        so.FindProperty("dampTimeNormal").floatValue = config.targetDampTimeNormal;
        so.FindProperty("dampTimeSlow").floatValue   = config.targetDampTimeSlow;
        so.FindProperty("dampTimeSlower").floatValue = config.targetDampTimeSlower;
        so.FindProperty("xLookAhead").floatValue     = config.xLookAhead;
        so.FindProperty("dashLookAhead").floatValue  = config.dashLookAhead;
        so.FindProperty("fallLookAhead").floatValue  = config.fallLookAhead;
        so.FindProperty("sprintLookAhead").floatValue = config.sprintLookAhead;
        so.FindProperty("specialMoveLookAhead").floatValue = config.specialMoveLookAhead;
        so.FindProperty("baseVerticalOffset").floatValue = config.baseVerticalOffset;
        so.FindProperty("fastFallVerticalOffset").floatValue = config.fastFallVerticalOffset;
        so.FindProperty("fallCatchAccel").floatValue = config.fallCatchAccel;
        so.FindProperty("fallCatchMax").floatValue   = config.fallCatchMax;
        so.ApplyModifiedProperties();
    }

    private static void ApplyCameraControllerTuning(CameraController c, CameraConfig config)
    {
        SerializedObject so = new SerializedObject(c);
        so.FindProperty("dampTimeNormal").floatValue   = config.cameraDampTimeNormal;
        so.FindProperty("dampTimeSlow").floatValue     = config.cameraDampTimeSlow;
        so.FindProperty("maxVelocity").floatValue      = config.maxVelocity;
        so.FindProperty("lookOffset").floatValue       = 0f;
        so.FindProperty("fieldOfView").floatValue      = config.fieldOfView;
        so.FindProperty("cameraZ").floatValue          = config.cameraZ;
        so.FindProperty("startLockedTimer").floatValue = config.startLockedTimer;
        so.ApplyModifiedProperties();
    }

    private static CameraConfig GetOrCreateCameraConfig()
    {
        CameraConfig config = AssetDatabase.LoadAssetAtPath<CameraConfig>(CameraConfigPath);
        if (config != null)
            return config;

        Directory.CreateDirectory(Path.GetDirectoryName(CameraConfigPath));
        config = ScriptableObject.CreateInstance<CameraConfig>();
        AssetDatabase.CreateAsset(config, CameraConfigPath);
        AssetDatabase.SaveAssets();
        return config;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static GameObject GetOrCreateChild(GameObject parent, string name)
    {
        Transform existing = parent.transform.Find(name);
        if (existing != null) return existing.gameObject;

        GameObject child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "Create " + name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null)
            comp = Undo.AddComponent<T>(go);
        return comp;
    }
}
