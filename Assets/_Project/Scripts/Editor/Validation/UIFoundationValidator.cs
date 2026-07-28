using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// UI foundation validator (Packages A1 + A2). Checks the persistent MenuRoot composition, the
/// registered pausing roots, the seven-tab Gameplay Menu contract, the Gear display catalogue, the
/// production/Sandbox boundary, and semantic Canvas layering described in
/// Docs/FeatureSpecs/UIArchitecture.md, Docs/FeatureSpecs/GameplayMenu.md, and
/// Docs/FeatureSpecs/UISandbox.md.
/// </summary>
public static class UIFoundationValidator
{
    private const string GameCamerasPrefabPath = "Assets/_Project/Prefabs/Managers/_GameCameras.prefab";

    /// <summary>Canonical Sandbox scene path (relocated out of Scenes/Development/).</summary>
    private const string SandboxScenePath = "Assets/_Project/Scenes/UISandbox.unity";

    private const string InputActionsAssetPath = "Assets/_Project/Input/InputSystem_Actions.inputactions";
    private const string GearCatalogAssetPath = "Assets/_Project/ScriptableObjects/UI/Gear/GearDisplayCatalog.asset";
    private const string ProductionAbilityStatePath = "Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset";

    private static readonly (string field, string expectedActionPath)[] RequiredFlowActions =
    {
        ("pauseActionRef", "System/Pause"),
        ("gameplayMenuActionRef", "System/GameplayMenu"),
        ("uiCancelActionRef", "UI/Cancel"),
    };

    private static readonly (string field, string expectedActionPath)[] RequiredGameplayMenuActions =
    {
        ("previousTabActionRef", "UI/PreviousTab"),
        ("nextTabActionRef", "UI/NextTab"),
    };

    private static readonly (string field, string expectedActionPath)[] RequiredUiModuleActions =
    {
        ("m_PointAction", "UI/Point"),
        ("m_MoveAction", "UI/Navigate"),
        ("m_SubmitAction", "UI/Submit"),
        ("m_CancelAction", "UI/Cancel"),
        ("m_LeftClickAction", "UI/Click"),
        ("m_RightClickAction", "UI/RightClick"),
        ("m_MiddleClickAction", "UI/MiddleClick"),
        ("m_ScrollWheelAction", "UI/ScrollWheel"),
        ("m_TrackedDevicePositionAction", "UI/TrackedDevicePosition"),
        ("m_TrackedDeviceOrientationAction", "UI/TrackedDeviceOrientation"),
    };

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
        issues += ValidateGearCatalog();

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
            else
            {
                InputSystemUIInputModule productionModule = eventSystems[0].GetComponentInChildren<InputSystemUIInputModule>(true);
                if (productionModule == null)
                {
                    Debug.LogError("[UIFoundationValidator] Production EventSystem has no InputSystemUIInputModule.");
                    issues++;
                }
                else
                {
                    issues += ValidateInputModuleWiring(productionModule, "Production");
                }
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
                issues += RequireReference(flowSo, "gameplayMenuRootBehaviour", "UIFlowController.gameplayMenuRootBehaviour");

                SerializedProperty gameplayMenuProp = flowSo.FindProperty("gameplayMenuRootBehaviour");
                Object gameplayMenuRoot = gameplayMenuProp != null ? gameplayMenuProp.objectReferenceValue : null;
                if (gameplayMenuRoot != null && !(gameplayMenuRoot is IUIFlowRootScreen))
                {
                    Debug.LogError($"[UIFoundationValidator] UIFlowController.gameplayMenuRootBehaviour ('{gameplayMenuRoot.GetType().Name}') does not implement IUIFlowRootScreen.");
                    issues++;
                }

                foreach ((string field, string expectedActionPath) in RequiredFlowActions)
                {
                    issues += ValidateActionReference(flowSo, field, expectedActionPath, "UIFlowController");
                }
            }

            issues += ValidateGameplayMenuScreen(menuRoot);

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

            issues += RejectSandboxOnlyComponents(root.transform);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return issues;
    }

    /// <summary>
    /// Package A2 contract: exactly one Gameplay Menu root under MenuRoot, hidden by default, with
    /// exactly seven registrations in the fixed order, every view implementing
    /// <see cref="IGameplayMenuTab"/>, a resolvable selection for every tab, and durable
    /// Previous/Next tab action references.
    /// </summary>
    private static int ValidateGameplayMenuScreen(Transform menuRoot)
    {
        GameplayMenuScreen[] screens = menuRoot.GetComponentsInChildren<GameplayMenuScreen>(true);
        if (screens.Length == 0)
        {
            Debug.LogError("[UIFoundationValidator] MenuRoot contains no GameplayMenuScreen; Package A2 requires the Gameplay Menu root under RootInterfaceLayer.");
            return 1;
        }

        if (screens.Length > 1)
        {
            Debug.LogError($"[UIFoundationValidator] MenuRoot contains {screens.Length} GameplayMenuScreen components; exactly one persistent root is allowed.");
            return 1;
        }

        int issues = 0;
        GameplayMenuScreen screen = screens[0];

        Transform rootLayer = menuRoot.Find("RootInterfaceLayer");
        if (rootLayer == null || !screen.transform.IsChildOf(rootLayer))
        {
            Debug.LogError("[UIFoundationValidator] GameplayMenuScreen must be a sibling of PauseMenuScreen under MenuRoot/RootInterfaceLayer.");
            issues++;
        }

        SerializedObject so = new SerializedObject(screen);
        issues += RequireReference(so, "visualRoot", "GameplayMenuScreen.visualRoot");
        issues += RequireReference(so, "eventSystem", "GameplayMenuScreen.eventSystem");
        issues += RequireReference(so, "closeButton", "GameplayMenuScreen.closeButton");
        issues += RequireReference(so, "inputActions", "GameplayMenuScreen.inputActions");
        issues += RequireReference(so, "activeTabTitleLabel", "GameplayMenuScreen.activeTabTitleLabel");
        issues += RequireReference(so, "stripViewport", "GameplayMenuScreen.stripViewport");
        issues += ValidateTopTabStrip(screen);

        foreach ((string field, string expectedActionPath) in RequiredGameplayMenuActions)
        {
            issues += ValidateActionReference(so, field, expectedActionPath, "GameplayMenuScreen");
        }

        SerializedProperty visualRootProp = so.FindProperty("visualRoot");
        GameObject visualRoot = visualRootProp != null ? visualRootProp.objectReferenceValue as GameObject : null;
        if (visualRoot != null && visualRoot.activeSelf)
        {
            Debug.LogError("[UIFoundationValidator] GameplayMenuScreen's visual root must default to inactive so a closed menu never blocks HUD raycasts.");
            issues++;
        }

        SerializedProperty tabs = so.FindProperty("tabs");
        if (tabs == null || tabs.arraySize != GameplayMenuScreen.FixedTabOrder.Length)
        {
            Debug.LogError($"[UIFoundationValidator] GameplayMenuScreen must register exactly {GameplayMenuScreen.FixedTabOrder.Length} tabs; found {(tabs == null ? 0 : tabs.arraySize)}.");
            return issues + 1;
        }

        HashSet<int> seenIds = new HashSet<int>();
        for (int i = 0; i < tabs.arraySize; i++)
        {
            SerializedProperty element = tabs.GetArrayElementAtIndex(i);
            int id = element.FindPropertyRelative("id").enumValueIndex;
            GameplayMenuTabId expected = GameplayMenuScreen.FixedTabOrder[i];

            if (id != (int)expected)
            {
                Debug.LogError($"[UIFoundationValidator] Gameplay Menu tab {i} is '{(GameplayMenuTabId)id}'; the confirmed order requires '{expected}'. All seven tabs must stay visible and in order.");
                issues++;
            }

            if (!seenIds.Add(id))
            {
                Debug.LogError($"[UIFoundationValidator] Gameplay Menu tab '{(GameplayMenuTabId)id}' is registered more than once.");
                issues++;
            }

            Object button = element.FindPropertyRelative("tabButton").objectReferenceValue;
            if (button == null)
            {
                Debug.LogError($"[UIFoundationValidator] Gameplay Menu tab '{(GameplayMenuTabId)id}' has no tab button; every confirmed tab must be reachable.");
                issues++;
            }
            else
            {
                Button uiButton = ((GameplayMenuTabButton)button).Button;
                if (uiButton == null || !uiButton.interactable)
                {
                    Debug.LogError($"[UIFoundationValidator] Gameplay Menu tab '{(GameplayMenuTabId)id}' button is missing or non-interactable; confirmed tabs are never disabled.");
                    issues++;
                }
            }

            Object view = element.FindPropertyRelative("tabViewBehaviour").objectReferenceValue;
            if (view == null)
            {
                Debug.LogError($"[UIFoundationValidator] Gameplay Menu tab '{(GameplayMenuTabId)id}' has no tab view assigned.");
                issues++;
                continue;
            }

            if (!(view is IGameplayMenuTab))
            {
                Debug.LogError($"[UIFoundationValidator] Gameplay Menu tab '{(GameplayMenuTabId)id}' view '{view.GetType().Name}' does not implement IGameplayMenuTab.");
                issues++;
                continue;
            }

            issues += RejectGameplayOwnershipReferences((MonoBehaviour)view, (GameplayMenuTabId)id);

            if (view is GameplayMenuEmptyTabView emptyTab)
            {
                if (emptyTab.ContentRoot == null)
                {
                    Debug.LogError($"[UIFoundationValidator] Empty-state tab '{(GameplayMenuTabId)id}' has no content root.");
                    issues++;
                }

                issues += ValidateEmptyStateCopy((GameplayMenuTabId)id, emptyTab.AuthoredTitle, "title");
                issues += ValidateEmptyStateCopy((GameplayMenuTabId)id, emptyTab.AuthoredBody, "body");
            }
        }

        issues += ValidateGearTab(screen);
        return issues;
    }

    /// <summary>
    /// Package A2 correction pass: the tab presentation is a top-centred horizontal strip, not a
    /// left vertical rail. Checks the strip is authored above the content region, is laid out
    /// horizontally, holds all seven cells in order, and is flanked by Previous/Next hints.
    /// </summary>
    private static int ValidateTopTabStrip(GameplayMenuScreen screen)
    {
        int issues = 0;

        RectTransform viewport = new SerializedObject(screen).FindProperty("stripViewport").objectReferenceValue as RectTransform;
        if (viewport == null)
        {
            return issues; // already reported by RequireReference
        }

        HorizontalLayoutGroup horizontal = viewport.GetComponentInChildren<HorizontalLayoutGroup>(true);
        if (horizontal == null)
        {
            Debug.LogError("[UIFoundationValidator] The tab strip must be laid out by a HorizontalLayoutGroup under GameplayMenuScreen.stripViewport; the left vertical rail is superseded.");
            issues++;
        }
        else if (horizontal.GetComponent<VerticalLayoutGroup>() != null)
        {
            Debug.LogError("[UIFoundationValidator] The tab strip container still has a VerticalLayoutGroup; the tab presentation is a top-centred horizontal strip.");
            issues++;
        }

        GameplayMenuTabButton[] cells = viewport.GetComponentsInChildren<GameplayMenuTabButton>(true);
        if (cells.Length != GameplayMenuScreen.FixedTabOrder.Length)
        {
            Debug.LogError($"[UIFoundationValidator] The tab strip holds {cells.Length} cell(s); all {GameplayMenuScreen.FixedTabOrder.Length} confirmed tabs must live in the strip and stay visible.");
            issues++;
        }

        foreach (GameplayMenuTabButton cell in cells)
        {
            if (!cell.gameObject.activeSelf)
            {
                Debug.LogError($"[UIFoundationValidator] Tab strip cell '{cell.name}' is inactive; narrow aspect ratios collapse a cell's title, never the cell itself.");
                issues++;
            }

            if (cell.ExpandedWidth <= 0f)
            {
                Debug.LogError($"[UIFoundationValidator] Tab strip cell '{cell.name}' has no expanded width, so the strip cannot decide when to collapse its titles.");
                issues++;
            }
        }

        Transform header = viewport.parent;
        if (header == null || header.Find("PreviousTabHint") == null || header.Find("NextTabHint") == null)
        {
            Debug.LogError("[UIFoundationValidator] The tab strip must be flanked by authored PreviousTabHint and NextTabHint controls.");
            issues++;
        }

        return issues;
    }

    /// <summary>
    /// Empty states are authored player-facing content. Reject developer wording outright — it is
    /// the single most likely way an unfinished tab starts reading as a bug.
    /// </summary>
    private static int ValidateEmptyStateCopy(GameplayMenuTabId tabId, string copy, string label)
    {
        if (string.IsNullOrWhiteSpace(copy))
        {
            Debug.LogError($"[UIFoundationValidator] Empty-state tab '{tabId}' has no authored {label} copy.");
            return 1;
        }

        string[] banned = { "coming soon", "not implemented", "todo", "tbd", "placeholder", "wip", "n/a" };
        string lowered = copy.ToLowerInvariant();
        foreach (string phrase in banned)
        {
            if (lowered.Contains(phrase))
            {
                Debug.LogError($"[UIFoundationValidator] Empty-state tab '{tabId}' {label} copy contains developer wording ('{phrase}'): \"{copy}\".");
                return 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// A tab presenter must never serialize a gameplay owner, save API, or scene-flow service —
    /// that is how presentation quietly becomes domain ownership.
    /// </summary>
    private static int RejectGameplayOwnershipReferences(MonoBehaviour view, GameplayMenuTabId tabId)
    {
        int issues = 0;
        SerializedObject so = new SerializedObject(view);
        SerializedProperty iterator = so.GetIterator();
        while (iterator.NextVisible(true))
        {
            if (iterator.propertyType != SerializedPropertyType.ObjectReference || iterator.objectReferenceValue == null)
            {
                continue;
            }

            string typeName = iterator.objectReferenceValue.GetType().Name;
            if (typeName == "SaveManager" || typeName == "HeroInputReader" || typeName == "SceneTransitionManager"
                || typeName == "WorldStateRegistry" || typeName == "GameManager" || typeName == "UISandboxController")
            {
                Debug.LogError($"[UIFoundationValidator] Gameplay Menu tab '{tabId}' serializes a '{typeName}' reference on '{view.GetType().Name}.{iterator.name}'. Tab presenters own presentation only.");
                issues++;
            }
        }

        return issues;
    }

    private static int ValidateGearTab(GameplayMenuScreen screen)
    {
        GearScreen gear = screen.GetComponentInChildren<GearScreen>(true);
        if (gear == null)
        {
            Debug.LogError("[UIFoundationValidator] The Gameplay Menu has no GearScreen; Gear is the one data-backed Package A2 tab.");
            return 1;
        }

        int issues = 0;
        SerializedObject so = new SerializedObject(gear);
        issues += RequireReference(so, "contentRoot", "GearScreen.contentRoot");
        issues += RequireReference(so, "entryContainer", "GearScreen.entryContainer");
        issues += RequireReference(so, "entryPrefab", "GearScreen.entryPrefab");
        issues += RequireReference(so, "detailsPanel", "GearScreen.detailsPanel");
        issues += RequireReference(so, "emptyStateRoot", "GearScreen.emptyStateRoot");
        issues += RequireReference(so, "populatedRoot", "GearScreen.populatedRoot");

        SerializedProperty stateProp = so.FindProperty("abilityState");
        Object state = stateProp != null ? stateProp.objectReferenceValue : null;
        if (state == null)
        {
            Debug.LogError("[UIFoundationValidator] GearScreen.abilityState is not assigned; Gear must read the production PlayerAbilityState.");
            issues++;
        }
        else if (AssetDatabase.GetAssetPath(state) != ProductionAbilityStatePath)
        {
            Debug.LogError($"[UIFoundationValidator] GearScreen.abilityState must be '{ProductionAbilityStatePath}'; found '{AssetDatabase.GetAssetPath(state)}'.");
            issues++;
        }

        SerializedProperty catalogProp = so.FindProperty("catalog");
        Object catalog = catalogProp != null ? catalogProp.objectReferenceValue : null;
        if (catalog == null)
        {
            Debug.LogError("[UIFoundationValidator] GearScreen.catalog is not assigned; assign the production GearDisplayCatalog (an empty catalogue is valid).");
            issues++;
        }
        else if (AssetDatabase.GetAssetPath(catalog) != GearCatalogAssetPath)
        {
            Debug.LogError($"[UIFoundationValidator] GearScreen.catalog must be the production catalogue at '{GearCatalogAssetPath}'; found '{AssetDatabase.GetAssetPath(catalog)}'. Sandbox fixture catalogues must never enter production.");
            issues++;
        }

        return issues;
    }

    private static int ValidateGearCatalog()
    {
        GearDisplayCatalog catalog = AssetDatabase.LoadAssetAtPath<GearDisplayCatalog>(GearCatalogAssetPath);
        if (catalog == null)
        {
            Debug.LogError($"[UIFoundationValidator] Expected the production Gear catalogue at {GearCatalogAssetPath}.");
            return 1;
        }

        List<string> problems = new List<string>();
        catalog.CollectValidationIssues(problems);
        foreach (string problem in problems)
        {
            Debug.LogError($"[UIFoundationValidator] GearDisplayCatalog: {problem}");
        }

        return problems.Count;
    }

    private static int ValidateActionReference(SerializedObject so, string field, string expectedActionPath, string label)
    {
        SerializedProperty prop = so.FindProperty(field);
        InputActionReference reference = prop != null ? prop.objectReferenceValue as InputActionReference : null;
        if (reference == null)
        {
            Debug.LogError($"[UIFoundationValidator] {label}.{field} is not assigned; assign the durable '{expectedActionPath}' reference.");
            return 1;
        }

        InputAction action = reference.action;
        string actualActionPath = action != null && action.actionMap != null ? action.actionMap.name + "/" + action.name : null;
        if (actualActionPath != expectedActionPath)
        {
            Debug.LogError($"[UIFoundationValidator] {label}.{field} must resolve to '{expectedActionPath}'; found '{actualActionPath ?? "null"}'.");
            return 1;
        }

        return 0;
    }

    private static int RejectSandboxOnlyComponents(Transform root)
    {
        int issues = 0;
        if (root.GetComponentInChildren<UISandboxController>(true) != null)
        {
            Debug.LogError("[UIFoundationValidator] _GameCameras.prefab must not contain a UISandboxController; it is Sandbox-only.");
            issues++;
        }

        if (root.GetComponentInChildren<SandboxOptionsPreviewPanel>(true) != null)
        {
            Debug.LogError("[UIFoundationValidator] _GameCameras.prefab must not contain a SandboxOptionsPreviewPanel; the Options preview placeholder is Sandbox-only.");
            issues++;
        }

        if (root.GetComponentInChildren<SandboxQuitCallbackStatus>(true) != null)
        {
            Debug.LogError("[UIFoundationValidator] _GameCameras.prefab must not contain a SandboxQuitCallbackStatus; the Quit callback status label is Sandbox-only.");
            issues++;
        }

        if (root.GetComponentInChildren<SandboxDeveloperUtilityLayer>(true) != null)
        {
            Debug.LogError("[UIFoundationValidator] _GameCameras.prefab must not contain a SandboxDeveloperUtilityLayer; the emergency close/recovery controls are Sandbox-only and must never ship above the production Root/Modal layers.");
            issues++;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "DeveloperUtilityLayer")
            {
                Debug.LogError("[UIFoundationValidator] _GameCameras.prefab contains a 'DeveloperUtilityLayer'; the Sandbox developer layer must never enter production composition.");
                issues++;
            }
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

        InputSystemUIInputModule sandboxModule = null;
        EventSystem[] sandboxEventSystems = FindAllInScene<EventSystem>(scene);
        if (sandboxEventSystems.Length != 1)
        {
            Debug.LogError($"[UIFoundationValidator] UISandbox.unity must contain exactly one EventSystem; found {sandboxEventSystems.Length}.");
            issues++;
        }
        else
        {
            sandboxModule = sandboxEventSystems[0].GetComponentInChildren<InputSystemUIInputModule>(true);
        }

        if (sandboxModule == null)
        {
            Debug.LogError("[UIFoundationValidator] UISandbox.unity EventSystem has no InputSystemUIInputModule.");
            issues++;
        }
        else
        {
            issues += ValidateInputModuleWiring(sandboxModule, "Sandbox");
        }

        issues += ValidateSandboxRootFlowAndDeveloperLayer(scene, controller);

        // The Sandbox nests the real Gameplay Menu prefab, so its Gear tab must have the production
        // ability state and catalogue cleared — UISandboxController supplies runtime-created
        // fixtures instead, and the Sandbox must never mutate a production asset.
        foreach (GearScreen sandboxGear in FindAllInScene<GearScreen>(scene))
        {
            SerializedObject gearSo = new SerializedObject(sandboxGear);
            issues += RejectProductionReference(gearSo, "abilityState", "Sandbox GearScreen");

            SerializedProperty catalogProp = gearSo.FindProperty("catalog");
            if (catalogProp != null && catalogProp.objectReferenceValue != null
                && AssetDatabase.GetAssetPath(catalogProp.objectReferenceValue) == GearCatalogAssetPath)
            {
                Debug.LogError("[UIFoundationValidator] Sandbox GearScreen references the production GearDisplayCatalog; Sandbox fixtures must use a runtime-created catalogue.");
                issues++;
            }
        }

        return issues;
    }

    /// <summary>
    /// Package A2 correction pass (BUG 3/4). The Sandbox must drive root open/close through its own
    /// <see cref="UIFlowController"/> rather than a screen's Show/Hide, and its developer recovery
    /// controls must sit on a canvas above the production Root and Modal layers so a full-screen
    /// root can keep correctly blocking raycasts to everything beneath it.
    /// </summary>
    private static int ValidateSandboxRootFlowAndDeveloperLayer(Scene scene, UISandboxController controller)
    {
        int issues = 0;

        UIFlowController[] flows = FindAllInScene<UIFlowController>(scene);
        if (flows.Length != 1)
        {
            Debug.LogError($"[UIFoundationValidator] UISandbox.unity must contain exactly one UIFlowController so root open/close runs the production sequence; found {flows.Length}.");
            issues++;
        }
        else
        {
            SerializedObject flowSo = new SerializedObject(flows[0]);
            issues += RequireReference(flowSo, "inputActions", "Sandbox UIFlowController.inputActions");
            issues += RequireReference(flowSo, "eventSystem", "Sandbox UIFlowController.eventSystem");
            issues += RequireReference(flowSo, "pauseRootBehaviour", "Sandbox UIFlowController.pauseRootBehaviour");
            issues += RequireReference(flowSo, "gameplayMenuRootBehaviour", "Sandbox UIFlowController.gameplayMenuRootBehaviour");

            foreach ((string field, string expectedActionPath) in RequiredFlowActions)
            {
                issues += ValidateActionReference(flowSo, field, expectedActionPath, "Sandbox UIFlowController");
            }
        }

        if (controller != null)
        {
            SerializedObject controllerSo = new SerializedObject(controller);
            issues += RequireReference(controllerSo, "uiFlow", "UISandboxController.uiFlow");
            issues += RequireReference(controllerSo, "healthDamageOneButton", "UISandboxController.healthDamageOneButton");
            issues += RequireReference(controllerSo, "healthHealOneButton", "UISandboxController.healthHealOneButton");
            issues += RequireReference(controllerSo, "resourceAddButton", "UISandboxController.resourceAddButton");
            issues += RequireReference(controllerSo, "resourceSpendButton", "UISandboxController.resourceSpendButton");
            issues += RequireReference(controllerSo, "resourceClearButton", "UISandboxController.resourceClearButton");
        }

        SandboxDeveloperUtilityLayer[] devLayers = FindAllInScene<SandboxDeveloperUtilityLayer>(scene);
        if (devLayers.Length != 1)
        {
            Debug.LogError($"[UIFoundationValidator] UISandbox.unity must contain exactly one SandboxDeveloperUtilityLayer so a misauthored root can always be recovered; found {devLayers.Length}.");
            return issues + 1;
        }

        SerializedObject devSo = new SerializedObject(devLayers[0]);
        issues += RequireReference(devSo, "uiFlow", "SandboxDeveloperUtilityLayer.uiFlow");
        issues += RequireReference(devSo, "emergencyCloseButton", "SandboxDeveloperUtilityLayer.emergencyCloseButton");

        Canvas devCanvas = devLayers[0].GetComponent<Canvas>();
        if (devCanvas == null || devLayers[0].GetComponent<GraphicRaycaster>() == null)
        {
            Debug.LogError("[UIFoundationValidator] The Sandbox developer utility layer needs its own Canvas and GraphicRaycaster; otherwise an open root blocks the only recovery control.");
            return issues + 1;
        }

        // The emergency controls only work if they genuinely sort above every production layer.
        int highestProductionOrder = int.MinValue;
        string highestName = "none";
        foreach (Canvas canvas in FindAllInScene<Canvas>(scene))
        {
            if (canvas == devCanvas)
            {
                continue;
            }

            int order = canvas.sortingOrder;
            if (order > highestProductionOrder)
            {
                highestProductionOrder = order;
                highestName = canvas.name;
            }
        }

        if (devCanvas.sortingOrder <= highestProductionOrder)
        {
            Debug.LogError($"[UIFoundationValidator] The developer utility canvas sorts at {devCanvas.sortingOrder}, at or below '{highestName}' ({highestProductionOrder}). It must sort above every production layer to stay usable while a root is open.");
            issues++;
        }

        issues += ValidateSandboxSemanticLayering(scene);
        return issues;
    }

    /// <summary>
    /// The Sandbox previews the production raycast-blocking behaviour, so it must reproduce the same
    /// semantic ordering: fixture/HUD below the root, root below modal.
    /// </summary>
    private static int ValidateSandboxSemanticLayering(Scene scene)
    {
        Canvas sandbox = null;
        Canvas root = null;
        Canvas modal = null;
        foreach (Canvas canvas in FindAllInScene<Canvas>(scene))
        {
            if (canvas.name == "SandboxCanvas") sandbox = canvas;
            if (canvas.name == "RootInterfaceLayer") root = canvas;
            if (canvas.name == "ModalLayer") modal = canvas;
        }

        if (sandbox == null || root == null || modal == null)
        {
            Debug.LogError("[UIFoundationValidator] Could not resolve SandboxCanvas/RootInterfaceLayer/ModalLayer for Sandbox layering validation.");
            return 1;
        }

        int issues = 0;
        if (!(sandbox.sortingOrder < root.sortingOrder && root.sortingOrder < modal.sortingOrder))
        {
            Debug.LogError($"[UIFoundationValidator] Sandbox semantic sorting violated: Sandbox({sandbox.sortingOrder}) < Root({root.sortingOrder}) < Modal({modal.sortingOrder}) is required.");
            issues++;
        }

        // A nested Canvas ignores sortingOrder entirely unless it overrides sorting, which is
        // exactly how "the root renders above the fixture panel" silently stops being true.
        if (root.transform.parent != null && !root.overrideSorting)
        {
            Debug.LogError("[UIFoundationValidator] The Sandbox RootInterfaceLayer is a nested Canvas without Override Sorting, so its sorting order is ignored.");
            issues++;
        }

        if (modal.transform.parent != null && !modal.overrideSorting)
        {
            Debug.LogError("[UIFoundationValidator] The Sandbox ModalLayer is a nested Canvas without Override Sorting, so its sorting order is ignored.");
            issues++;
        }

        return issues;
    }

    private static int ValidateInputModuleWiring(InputSystemUIInputModule module, string label)
    {
        int issues = 0;
        SerializedObject so = new SerializedObject(module);

        SerializedProperty actionsAssetProp = so.FindProperty("m_ActionsAsset");
        InputActionAsset actionsAsset = actionsAssetProp != null ? actionsAssetProp.objectReferenceValue as InputActionAsset : null;
        string actionsAssetPath = actionsAsset != null ? AssetDatabase.GetAssetPath(actionsAsset) : null;
        if (actionsAssetPath != InputActionsAssetPath)
        {
            Debug.LogError($"[UIFoundationValidator] {label} InputSystemUIInputModule.actionsAsset must be '{InputActionsAssetPath}'; found '{actionsAssetPath ?? "null"}'.");
            issues++;
        }

        foreach ((string field, string expectedActionPath) in RequiredUiModuleActions)
        {
            SerializedProperty prop = so.FindProperty(field);
            InputActionReference reference = prop != null ? prop.objectReferenceValue as InputActionReference : null;
            if (reference == null)
            {
                Debug.LogError($"[UIFoundationValidator] {label} InputSystemUIInputModule.{field} is not assigned.");
                issues++;
                continue;
            }

            InputAction action = reference.action;
            string actualActionPath = action != null && action.actionMap != null ? action.actionMap.name + "/" + action.name : null;
            if (actualActionPath != expectedActionPath)
            {
                Debug.LogError($"[UIFoundationValidator] {label} InputSystemUIInputModule.{field} must resolve to '{expectedActionPath}'; found '{actualActionPath ?? "null"}'.");
                issues++;
            }
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
