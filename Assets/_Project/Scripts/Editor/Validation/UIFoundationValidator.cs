using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Package A1 UI foundation validator. Checks the persistent MenuRoot composition, the
/// production/Sandbox boundary, and semantic Canvas layering described in
/// Docs/FeatureSpecs/UIArchitecture.md and Docs/FeatureSpecs/UISandbox.md.
/// </summary>
public static class UIFoundationValidator
{
    private const string GameCamerasPrefabPath = "Assets/_Project/Prefabs/Managers/_GameCameras.prefab";
    private const string SandboxScenePath = "Assets/_Project/Scenes/Development/UISandbox.unity";

    private static readonly string[] ProductionStateAssetPaths =
    {
        "Assets/_Project/ScriptableObjects/Hero/PlayerHealthState.asset",
        "Assets/_Project/ScriptableObjects/Hero/PlayerResourceState.asset",
        "Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset",
    };

    [MenuItem("Tools/Project/Validate UI Foundation")]
    public static void Validate()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogWarning("[UIFoundationValidator] Validation cancelled because a loaded scene has unsaved changes.");
                return;
            }
        }

        int issues = 0;
        issues += ValidateSandboxBuildExclusion();
        issues += ValidateGameCamerasPrefab();

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            issues += ValidateNoCompetingEventSystemInGameplayScenes();
            issues += ValidateSandboxScene();
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
            Debug.Log("[UIFoundationValidator] UI foundation validation passed.");
        }
        else
        {
            Debug.LogWarning($"[UIFoundationValidator] Found {issues} issue(s). See earlier logs.");
        }
    }

    private static int ValidateSandboxBuildExclusion()
    {
        int issues = 0;
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (buildScene.enabled && buildScene.path == SandboxScenePath)
            {
                Debug.LogError("[UIFoundationValidator] UISandbox.unity must not be an enabled Build Settings scene.");
                issues++;
            }
        }

        if (!System.IO.File.Exists(SandboxScenePath))
        {
            Debug.LogError($"[UIFoundationValidator] Expected Sandbox scene not found at {SandboxScenePath}.");
            issues++;
        }

        return issues;
    }

    private static int ValidateGameCamerasPrefab()
    {
        int issues = 0;
        GameObject root = PrefabUtility.LoadPrefabContents(GameCamerasPrefabPath);
        try
        {
            Transform menuRoot = root.transform.Find("MenuRoot");
            if (menuRoot == null)
            {
                Debug.LogError("[UIFoundationValidator] _GameCameras.prefab has no MenuRoot.");
                return issues + 1;
            }

            EventSystem[] eventSystems = menuRoot.GetComponentsInChildren<EventSystem>(true);
            if (eventSystems.Length != 1)
            {
                Debug.LogError($"[UIFoundationValidator] MenuRoot must contain exactly one persistent EventSystem; found {eventSystems.Length}.");
                issues++;
            }

            UIFlowController flow = menuRoot.GetComponent<UIFlowController>();
            if (flow == null)
            {
                Debug.LogError("[UIFoundationValidator] MenuRoot is missing UIFlowController.");
                issues++;
            }
            else
            {
                SerializedObject flowSo = new SerializedObject(flow);
                issues += RequireReference(flowSo, "inputActions", "UIFlowController.inputActions");
                issues += RequireReference(flowSo, "eventSystem", "UIFlowController.eventSystem");
                issues += RequireReference(flowSo, "pauseRootBehaviour", "UIFlowController.pauseRootBehaviour");

                SerializedProperty gameplayMenuProp = flowSo.FindProperty("gameplayMenuRootBehaviour");
                if (gameplayMenuProp != null && gameplayMenuProp.objectReferenceValue != null)
                {
                    Debug.LogWarning("[UIFoundationValidator] gameplayMenuRootBehaviour is assigned; Package A1 expected this to remain unregistered until Package A2.");
                }
            }

            Transform rootLayerT = menuRoot.Find("RootInterfaceLayer");
            Transform modalLayerT = menuRoot.Find("ModalLayer");
            Canvas hudCanvas = FindDeep(root.transform, "HUD Canvas")?.GetComponent<Canvas>();
            Canvas fadeCanvas = FindDeep(root.transform, "FadeCanvas")?.GetComponent<Canvas>();

            issues += ValidateInteractiveCanvas(rootLayerT, "RootInterfaceLayer");
            issues += ValidateInteractiveCanvas(modalLayerT, "ModalLayer");

            if (rootLayerT != null && modalLayerT != null && hudCanvas != null)
            {
                Canvas rootCanvas = rootLayerT.GetComponent<Canvas>();
                Canvas modalCanvas = modalLayerT.GetComponent<Canvas>();
                if (!(hudCanvas.sortingOrder < rootCanvas.sortingOrder && rootCanvas.sortingOrder < modalCanvas.sortingOrder))
                {
                    Debug.LogError($"[UIFoundationValidator] Semantic sorting violated: HUD({hudCanvas.sortingOrder}) < Root({rootCanvas.sortingOrder}) < Modal({modalCanvas.sortingOrder}) is required.");
                    issues++;
                }

                if (fadeCanvas != null && fadeCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    Debug.LogWarning("[UIFoundationValidator] FadeCanvas is expected to be Screen Space - Overlay so it always renders above camera-space Modal/Root/HUD canvases.");
                }
            }
            else
            {
                Debug.LogError("[UIFoundationValidator] Could not resolve RootInterfaceLayer/ModalLayer/HUD Canvas for sorting validation.");
                issues++;
            }

            // Hidden-by-default presentation must not block raycasts (an inactive Graphic simply
            // cannot be hit by GraphicRaycaster, but assert the authored default explicitly).
            PauseMenuScreen pauseScreen = menuRoot.GetComponentInChildren<PauseMenuScreen>(true);
            if (pauseScreen != null && pauseScreen.gameObject.activeInHierarchy)
            {
                Transform panel = pauseScreen.transform.Find("Panel");
                if (panel != null && panel.gameObject.activeSelf)
                {
                    Debug.LogError("[UIFoundationValidator] PauseMenuScreen's Panel must default to inactive so it never blocks HUD raycasts while closed.");
                    issues++;
                }
            }

            ConfirmationModal modal = menuRoot.GetComponentInChildren<ConfirmationModal>(true);
            if (modal != null)
            {
                Transform panel = modal.transform.Find("Panel");
                if (panel != null && panel.gameObject.activeSelf)
                {
                    Debug.LogError("[UIFoundationValidator] ConfirmationModal's Panel must default to inactive so it never blocks input while closed.");
                    issues++;
                }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return issues;
    }

    private static int ValidateInteractiveCanvas(Transform layer, string label)
    {
        if (layer == null)
        {
            Debug.LogError($"[UIFoundationValidator] MenuRoot is missing {label}.");
            return 1;
        }

        int issues = 0;
        if (layer.GetComponent<Canvas>() == null)
        {
            Debug.LogError($"[UIFoundationValidator] {label} has no Canvas.");
            issues++;
        }

        if (layer.GetComponent<GraphicRaycaster>() == null)
        {
            Debug.LogError($"[UIFoundationValidator] {label} has no GraphicRaycaster; it would never receive UI input.");
            issues++;
        }

        return issues;
    }

    private static int RequireReference(SerializedObject so, string propertyName, string label)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null || prop.objectReferenceValue == null)
        {
            Debug.LogError($"[UIFoundationValidator] {label} is not assigned.");
            return 1;
        }

        return 0;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                return t;
            }
        }

        return null;
    }

    private static int ValidateNoCompetingEventSystemInGameplayScenes()
    {
        int issues = 0;
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path))
            {
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            List<EventSystem> found = new List<EventSystem>();
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                found.AddRange(rootGo.GetComponentsInChildren<EventSystem>(true));
            }

            if (found.Count > 0)
            {
                Debug.LogError($"[UIFoundationValidator] Gameplay scene '{scene.name}' contains {found.Count} scene-local EventSystem(s); the persistent MenuRoot EventSystem must be the only one.");
                issues += found.Count;
            }
        }

        return issues;
    }

    private static int ValidateSandboxScene()
    {
        if (!System.IO.File.Exists(SandboxScenePath))
        {
            return 0; // already reported by ValidateSandboxBuildExclusion
        }

        int issues = 0;
        Scene scene = EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Single);

        HealthDisplay[] healthDisplays = FindAllInScene<HealthDisplay>(scene);
        ResourceDisplay[] resourceDisplays = FindAllInScene<ResourceDisplay>(scene);
        PersistentHudRoot[] hudRoots = FindAllInScene<PersistentHudRoot>(scene);

        foreach (HealthDisplay hd in healthDisplays)
        {
            issues += RejectProductionReference(new SerializedObject(hd), "healthState", "Sandbox HealthDisplay");
        }

        foreach (ResourceDisplay rd in resourceDisplays)
        {
            issues += RejectProductionReference(new SerializedObject(rd), "resourceState", "Sandbox ResourceDisplay");
        }

        if (hudRoots.Length > 0)
        {
            Debug.LogError("[UIFoundationValidator] UISandbox.unity must not instantiate a second production PersistentHudRoot.");
            issues += hudRoots.Length;
        }

        UISandboxController controller = null;
        foreach (GameObject rootGo in scene.GetRootGameObjects())
        {
            controller = rootGo.GetComponentInChildren<UISandboxController>(true);
            if (controller != null) break;
        }

        if (controller == null)
        {
            Debug.LogError("[UIFoundationValidator] UISandbox.unity has no UISandboxController.");
            issues++;
        }

        return issues;
    }

    private static int RejectProductionReference(SerializedObject so, string propertyName, string label)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null || prop.objectReferenceValue == null)
        {
            return 0;
        }

        string path = AssetDatabase.GetAssetPath(prop.objectReferenceValue);
        foreach (string productionPath in ProductionStateAssetPaths)
        {
            if (path == productionPath)
            {
                Debug.LogError($"[UIFoundationValidator] {label} references a production state asset ({path}); Sandbox fixtures must use isolated runtime instances only.");
                return 1;
            }
        }

        return 0;
    }

    private static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        List<T> found = new List<T>();
        foreach (GameObject rootGo in scene.GetRootGameObjects())
        {
            found.AddRange(rootGo.GetComponentsInChildren<T>(true));
        }

        return found.ToArray();
    }
}
