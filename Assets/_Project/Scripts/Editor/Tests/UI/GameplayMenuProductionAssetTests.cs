using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Asserts the Package A2.1 contract directly against the shipped production assets, so a prefab or
/// Input Action edit that silently breaks the Gameplay Menu fails here rather than only when
/// someone remembers to run Tools/Project/Validate UI Foundation.
///
/// These are asset-contract tests. The validator remains the authoring-time tool with rich
/// diagnostics; this suite is the regression net for the same rules.
/// </summary>
public sealed class GameplayMenuProductionAssetTests
{
    private const string GameCamerasPath = "Assets/_Project/Prefabs/Managers/_GameCameras.prefab";
    private const string GameplayMenuPrefabPath = "Assets/_Project/Prefabs/UI/GameplayMenuScreen.prefab";
    private const string SandboxScenePath = "Assets/_Project/Scenes/UISandbox.unity";
    private const string InputActionsPath = "Assets/_Project/Input/InputSystem_Actions.inputactions";
    private const string GearCatalogPath = "Assets/_Project/ScriptableObjects/UI/Gear/GearDisplayCatalog.asset";
    private const string AbilityStatePath = "Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset";

    private static readonly string[] TabPrefabPaths =
    {
        "Assets/_Project/Prefabs/UI/GameplayMenu/GearTab.prefab",
        "Assets/_Project/Prefabs/UI/GameplayMenu/CombatLoadoutTab.prefab",
        "Assets/_Project/Prefabs/UI/GameplayMenu/SatchelTab.prefab",
        "Assets/_Project/Prefabs/UI/GameplayMenu/FieldNotesTab.prefab",
        "Assets/_Project/Prefabs/UI/GameplayMenu/MapTab.prefab",
    };

    private GameObject cameras;

    [SetUp]
    public void SetUp()
    {
        cameras = PrefabUtility.LoadPrefabContents(GameCamerasPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (cameras != null) PrefabUtility.UnloadPrefabContents(cameras);
    }

    private GameplayMenuScreen Screen()
    {
        GameplayMenuScreen screen = cameras.GetComponentInChildren<GameplayMenuScreen>(true);
        Assert.That(screen, Is.Not.Null, "_GameCameras.prefab contains no GameplayMenuScreen.");
        return screen;
    }

    private static SerializedProperty Tabs(GameplayMenuScreen screen)
    {
        SerializedProperty tabs = new SerializedObject(screen).FindProperty("tabs");
        Assert.That(tabs, Is.Not.Null);
        return tabs;
    }

    // -------------------------------------------------------------------------
    // Persistent composition
    // -------------------------------------------------------------------------

    [Test]
    public void GameplayMenuIsRegisteredAsTheSecondPausingRoot()
    {
        Transform menuRoot = cameras.transform.Find("MenuRoot");
        Assert.That(menuRoot, Is.Not.Null);

        SerializedObject flow = new SerializedObject(menuRoot.GetComponent<UIFlowController>());
        Object registered = flow.FindProperty("gameplayMenuRootBehaviour").objectReferenceValue;

        Assert.That(registered, Is.Not.Null, "UIFlowController.gameplayMenuRootBehaviour must be assigned in Package A2.");
        Assert.That(registered, Is.InstanceOf<IUIFlowRootScreen>());
        Assert.That(registered, Is.SameAs(Screen()));
    }

    [Test]
    public void GameplayMenuIsASiblingOfPauseUnderRootInterfaceLayer()
    {
        Transform rootLayer = cameras.transform.Find("MenuRoot/RootInterfaceLayer");
        Assert.That(rootLayer, Is.Not.Null);
        Assert.That(Screen().transform.parent, Is.SameAs(rootLayer));
        Assert.That(rootLayer.GetComponentInChildren<PauseMenuScreen>(true), Is.Not.Null,
            "Pause and Gameplay Menu share RootInterfaceLayer; neither may move to HUDRoot or ModalLayer.");
    }

    [Test]
    public void ThereIsExactlyOnePersistentEventSystemAndOneOfEachRoot()
    {
        Assert.That(cameras.GetComponentsInChildren<EventSystem>(true).Length, Is.EqualTo(1));
        Assert.That(cameras.GetComponentsInChildren<UIFlowController>(true).Length, Is.EqualTo(1));
        Assert.That(cameras.GetComponentsInChildren<GameplayMenuScreen>(true).Length, Is.EqualTo(1));
        Assert.That(cameras.GetComponentsInChildren<PauseMenuScreen>(true).Length, Is.EqualTo(1));
    }

    [Test]
    public void GameplayMenuPanelDefaultsToInactiveSoItNeverBlocksHudRaycasts()
    {
        SerializedObject so = new SerializedObject(Screen());
        GameObject visualRoot = so.FindProperty("visualRoot").objectReferenceValue as GameObject;

        Assert.That(visualRoot, Is.Not.Null);
        Assert.That(visualRoot.activeSelf, Is.False);
    }

    [Test]
    public void ProductionPauseCloseAndModalReferencesAreShippedAndPanelDefaultsClosed()
    {
        PauseMenuScreen pause = cameras.GetComponentInChildren<PauseMenuScreen>(true);
        Assert.That(pause, Is.Not.Null);

        SerializedObject so = new SerializedObject(pause);
        GameObject visualRoot = so.FindProperty("visualRoot").objectReferenceValue as GameObject;
        Button continueButton = so.FindProperty("continueButton").objectReferenceValue as Button;
        ConfirmationModal modal =
            so.FindProperty("quitConfirmationModal").objectReferenceValue as ConfirmationModal;
        EventSystem eventSystem = so.FindProperty("eventSystem").objectReferenceValue as EventSystem;

        Assert.That(visualRoot, Is.Not.Null);
        Assert.That(visualRoot.activeSelf, Is.False);
        Assert.That(continueButton, Is.Not.Null);
        Assert.That(modal, Is.Not.Null,
            "The production nested Pause instance must reference the ModalLayer confirmation screen.");
        Assert.That(eventSystem, Is.SameAs(cameras.GetComponentInChildren<EventSystem>(true)));
    }

    [Test]
    public void OpenProductionRootsRemainFullScreenRaycastBlockers()
    {
        foreach (MonoBehaviour root in new MonoBehaviour[] {
            cameras.GetComponentInChildren<PauseMenuScreen>(true),
            Screen()
        })
        {
            GameObject visualRoot = new SerializedObject(root)
                .FindProperty("visualRoot").objectReferenceValue as GameObject;
            Graphic blocker = visualRoot != null ? visualRoot.GetComponent<Graphic>() : null;
            Assert.That(blocker, Is.Not.Null, root.GetType().Name + " visual root needs a full-screen blocker.");
            Assert.That(blocker.raycastTarget, Is.True,
                root.GetType().Name + " must block clicks to underlying gameplay UI while open.");
        }
    }

    [Test]
    public void SemanticLayeringIsPreserved()
    {
        Canvas hud = FindCanvas("HUD Canvas");
        Canvas root = FindCanvas("RootInterfaceLayer");
        Canvas modal = FindCanvas("ModalLayer");
        Canvas fade = FindCanvas("FadeCanvas");

        Assert.That(hud.sortingOrder, Is.LessThan(root.sortingOrder));
        Assert.That(root.sortingOrder, Is.LessThan(modal.sortingOrder));
        Assert.That(fade.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay),
            "Fade stays above camera-space Modal/Root/HUD.");
    }

    private Canvas FindCanvas(string name)
    {
        foreach (Transform t in cameras.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                Canvas canvas = t.GetComponent<Canvas>();
                if (canvas != null) return canvas;
            }
        }

        Assert.Fail("Canvas not found: " + name);
        return null;
    }

    // -------------------------------------------------------------------------
    // Five visible tabs
    // -------------------------------------------------------------------------

    [Test]
    public void ExactlyFiveTabsAreRegisteredInTheConfirmedOrder()
    {
        SerializedProperty tabs = Tabs(Screen());
        Assert.That(tabs.arraySize, Is.EqualTo(GameplayMenuScreen.FixedTabOrder.Length));

        for (int i = 0; i < GameplayMenuScreen.FixedTabOrder.Length; i++)
        {
            int id = tabs.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue;
            Assert.That((GameplayMenuTabId)id, Is.EqualTo(GameplayMenuScreen.FixedTabOrder[i]),
                $"Tab {i} is out of the confirmed Gear/Loadout/Satchel/Field Notes/Map order.");
        }
    }

    [Test]
    public void ProductionRegistrationsUseTheApprovedDisplayNames()
    {
        string[] expected = { "Gear", "Loadout", "Satchel", "Field Notes", "Map" };
        SerializedProperty tabs = Tabs(Screen());

        Assert.That(tabs.arraySize, Is.EqualTo(expected.Length));
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.That(tabs.GetArrayElementAtIndex(i).FindPropertyRelative("displayName").stringValue,
                Is.EqualTo(expected[i]), $"Tab {i} has the wrong player-facing display name.");
        }
    }

    [Test]
    public void EveryTabHasAnInteractableButtonAndAValidPresenter()
    {
        SerializedProperty tabs = Tabs(Screen());
        for (int i = 0; i < tabs.arraySize; i++)
        {
            SerializedProperty element = tabs.GetArrayElementAtIndex(i);
            GameplayMenuTabId id = (GameplayMenuTabId)element.FindPropertyRelative("id").intValue;

            GameplayMenuTabButton button = element.FindPropertyRelative("tabButton").objectReferenceValue as GameplayMenuTabButton;
            Assert.That(button, Is.Not.Null, $"Tab '{id}' has no button.");
            Assert.That(button.Button, Is.Not.Null, $"Tab '{id}' button has no UGUI Button.");
            Assert.That(button.Button.interactable, Is.True, $"Tab '{id}' must never be disabled.");
            Assert.That(button.gameObject.activeSelf, Is.True, $"Tab '{id}' must never be hidden.");

            Object view = element.FindPropertyRelative("tabViewBehaviour").objectReferenceValue;
            Assert.That(view, Is.InstanceOf<IGameplayMenuTab>(), $"Tab '{id}' view does not implement IGameplayMenuTab.");
        }
    }

    [Test]
    public void TabStripUsesHorizontalExplicitNavigationAndKeepsEveryCellVisible()
    {
        MethodInfo buildNavigation = typeof(GameplayMenuScreen).GetMethod(
            "BuildStripNavigation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(buildNavigation, Is.Not.Null);
        buildNavigation.Invoke(Screen(), null);

        SerializedProperty tabs = Tabs(Screen());
        Button[] buttons = new Button[tabs.arraySize];

        for (int i = 0; i < tabs.arraySize; i++)
        {
            GameplayMenuTabButton tabButton = tabs.GetArrayElementAtIndex(i)
                .FindPropertyRelative("tabButton").objectReferenceValue as GameplayMenuTabButton;
            Assert.That(tabButton, Is.Not.Null);
            buttons[i] = tabButton.Button;
            Assert.That(buttons[i], Is.Not.Null);
            Assert.That(tabButton.gameObject.activeSelf, Is.True);
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            Navigation navigation = buttons[i].navigation;
            Assert.That(navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(navigation.selectOnLeft, Is.SameAs(buttons[(i - 1 + buttons.Length) % buttons.Length]));
            Assert.That(navigation.selectOnRight, Is.SameAs(buttons[(i + 1) % buttons.Length]));
        }

        SerializedObject screen = new SerializedObject(Screen());
        RectTransform viewport = screen.FindProperty("stripViewport").objectReferenceValue as RectTransform;
        Assert.That(viewport, Is.Not.Null);
        HorizontalLayoutGroup horizontal = viewport.GetComponentInChildren<HorizontalLayoutGroup>(true);
        Assert.That(horizontal, Is.Not.Null);
        Assert.That(horizontal.GetComponent<VerticalLayoutGroup>(), Is.Null,
            "The strip container is horizontal; vertical stacks inside individual tab cells are allowed.");
    }

    [Test]
    public void EverySiblingTabShipsAuthoredPlayerFacingEmptyCopy()
    {
        string[] bannedPhrases = { "coming soon", "not implemented", "todo", "tbd", "placeholder", "wip" };

        foreach (GameplayMenuEmptyTabView tab in Screen().GetComponentsInChildren<GameplayMenuEmptyTabView>(true))
        {
            Assert.That(tab.ContentRoot, Is.Not.Null, $"'{tab.name}' has no content root.");
            Assert.That(tab.AuthoredTitle, Is.Not.Null.And.Not.Empty, $"'{tab.name}' has no title copy.");
            Assert.That(tab.AuthoredBody, Is.Not.Null.And.Not.Empty, $"'{tab.name}' has no body copy.");

            foreach (string phrase in bannedPhrases)
            {
                Assert.That(tab.AuthoredTitle.ToLowerInvariant(), Does.Not.Contain(phrase), $"'{tab.name}' title");
                Assert.That(tab.AuthoredBody.ToLowerInvariant(), Does.Not.Contain(phrase), $"'{tab.name}' body");
            }
        }
    }

    [Test]
    public void FourSiblingTabsUseTheSharedEmptyPresenterAndGearIsTheOnlyDataBackedTab()
    {
        Assert.That(Screen().GetComponentsInChildren<GameplayMenuEmptyTabView>(true).Length, Is.EqualTo(4));
        Assert.That(Screen().GetComponentsInChildren<GearScreen>(true).Length, Is.EqualTo(1));
    }

    [Test]
    public void EachTabPrefabExistsAndIsHiddenByDefault()
    {
        foreach (string path in TabPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, "Missing tab prefab: " + path);

            Transform content = prefab.transform.Find("Content");
            Assert.That(content, Is.Not.Null, "Missing Content root in " + path);
            Assert.That(content.gameObject.activeSelf, Is.False,
                "Tab content must default to inactive so only the active tab is ever visible: " + path);
        }
    }

    [Test]
    public void SupersededTopLevelTabPrefabPathsNoLongerExist()
    {
        string[] obsoletePaths =
        {
            "Assets/_Project/Prefabs/UI/GameplayMenu/ToolsTab.prefab",
            "Assets/_Project/Prefabs/UI/GameplayMenu/RecipesTab.prefab",
            "Assets/_Project/Prefabs/UI/GameplayMenu/TasksTab.prefab",
            "Assets/_Project/Prefabs/UI/GameplayMenu/JournalTab.prefab",
        };

        foreach (string path in obsoletePaths)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Null,
                "Superseded top-level tab asset still exists: " + path);
        }
    }

    // -------------------------------------------------------------------------
    // Gear data boundary
    // -------------------------------------------------------------------------

    [Test]
    public void GearReadsTheProductionAbilityStateAndProductionCatalogue()
    {
        GearScreen gear = Screen().GetComponentInChildren<GearScreen>(true);
        SerializedObject so = new SerializedObject(gear);

        Object state = so.FindProperty("abilityState").objectReferenceValue;
        Object catalog = so.FindProperty("catalog").objectReferenceValue;

        Assert.That(AssetDatabase.GetAssetPath(state), Is.EqualTo(AbilityStatePath));
        Assert.That(AssetDatabase.GetAssetPath(catalog), Is.EqualTo(GearCatalogPath));
    }

    [Test]
    public void NoTabPresenterSerializesAGameplayOwnerOrSaveApi()
    {
        string[] banned = { "SaveManager", "HeroInputReader", "SceneTransitionManager", "WorldStateRegistry", "GameManager", "UISandboxController" };

        foreach (MonoBehaviour view in Screen().GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!(view is IGameplayMenuTab)) continue;

            SerializedProperty iterator = new SerializedObject(view).GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference || iterator.objectReferenceValue == null) continue;

                string typeName = iterator.objectReferenceValue.GetType().Name;
                Assert.That(banned, Has.No.Member(typeName),
                    $"'{view.GetType().Name}.{iterator.name}' references a {typeName}; tabs own presentation only.");
            }
        }
    }

    [Test]
    public void MapTabReferencesNoWorldGraphOrVisitationData()
    {
        GameObject mapTab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/GameplayMenu/MapTab.prefab");
        Assert.That(mapTab, Is.Not.Null);

        foreach (MonoBehaviour view in mapTab.GetComponentsInChildren<MonoBehaviour>(true))
        {
            SerializedProperty iterator = new SerializedObject(view).GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference || iterator.objectReferenceValue == null) continue;

                string typeName = iterator.objectReferenceValue.GetType().Name;
                Assert.That(typeName, Is.Not.EqualTo("WorldStateRegistry"));
                Assert.That(typeName, Does.Not.Contain("WorldGraph"),
                    "A2 must not turn WGE editor data into player-map identity or geometry.");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Input
    // -------------------------------------------------------------------------

    [Test]
    public void TabCyclingActionsExistOnTheUiMapAndAreNotBoundToTab()
    {
        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        Assert.That(asset, Is.Not.Null);

        InputActionMap ui = asset.FindActionMap("UI", false);
        Assert.That(ui, Is.Not.Null);

        foreach (string actionName in new[] { "PreviousTab", "NextTab" })
        {
            InputAction action = ui.FindAction(actionName, false);
            Assert.That(action, Is.Not.Null, $"UI/{actionName} is missing.");
            Assert.That(action.bindings.Count, Is.GreaterThan(0), $"UI/{actionName} has no bindings.");

            foreach (InputBinding binding in action.bindings)
            {
                Assert.That(binding.path.ToLowerInvariant(), Is.Not.EqualTo("<keyboard>/tab"),
                    "Keyboard Tab stays reserved for the future Quick Map.");
            }
        }
    }

    [Test]
    public void GameplayMenuActionKeepsKeyboardIAndGainsAControllerBinding()
    {
        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        InputAction action = asset.FindActionMap("System", true).FindAction("GameplayMenu", true);

        bool hasKeyboardI = false;
        bool hasGamepad = false;
        foreach (InputBinding binding in action.bindings)
        {
            if (binding.path == "<Keyboard>/i") hasKeyboardI = true;
            if (binding.path.StartsWith("<Gamepad>/")) hasGamepad = true;
        }

        Assert.That(hasKeyboardI, Is.True, "Package A1's keyboard I binding must be preserved.");
        Assert.That(hasGamepad, Is.True, "Package A2 adds a provisional controller binding.");
    }

    [Test]
    public void GameplayPreviousAndNextAreNotReusedForTabCycling()
    {
        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        GameplayMenuScreen screen = Screen();
        SerializedObject so = new SerializedObject(screen);

        foreach (string field in new[] { "previousTabActionRef", "nextTabActionRef" })
        {
            InputActionReference reference = so.FindProperty(field).objectReferenceValue as InputActionReference;
            Assert.That(reference, Is.Not.Null, $"GameplayMenuScreen.{field} must use a durable reference.");
            Assert.That(reference.action.actionMap.name, Is.EqualTo("UI"),
                "Tab cycling lives on the UI map, never on gameplay Player/Previous or Player/Next.");
        }
    }

    [Test]
    public void UiFlowControllerUsesDurableActionReferences()
    {
        Transform menuRoot = cameras.transform.Find("MenuRoot");
        SerializedObject flow = new SerializedObject(menuRoot.GetComponent<UIFlowController>());

        (string field, string expected)[] expectations =
        {
            ("pauseActionRef", "System/Pause"),
            ("gameplayMenuActionRef", "System/GameplayMenu"),
            ("uiCancelActionRef", "UI/Cancel"),
        };

        foreach ((string field, string expected) in expectations)
        {
            InputActionReference reference = flow.FindProperty(field).objectReferenceValue as InputActionReference;
            Assert.That(reference, Is.Not.Null, $"UIFlowController.{field} is unassigned.");
            Assert.That(reference.action.actionMap.name + "/" + reference.action.name, Is.EqualTo(expected));
        }
    }

    [Test]
    public void PersistentInputModuleStillResolvesAllTenUiActions()
    {
        InputSystemUIInputModule module = cameras.GetComponentInChildren<InputSystemUIInputModule>(true);
        Assert.That(module, Is.Not.Null);

        (string field, string expected)[] expectations =
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

        SerializedObject so = new SerializedObject(module);
        foreach ((string field, string expected) in expectations)
        {
            InputActionReference reference = so.FindProperty(field).objectReferenceValue as InputActionReference;
            Assert.That(reference, Is.Not.Null, $"Input module {field} lost its reference.");
            Assert.That(reference.action.actionMap.name + "/" + reference.action.name, Is.EqualTo(expected));
        }
    }

    // -------------------------------------------------------------------------
    // Production / Sandbox boundary
    // -------------------------------------------------------------------------

    [Test]
    public void ProductionCompositionContainsNoSandboxComponents()
    {
        Assert.That(cameras.GetComponentInChildren<UISandboxController>(true), Is.Null);
        Assert.That(cameras.GetComponentInChildren<SandboxOptionsPreviewPanel>(true), Is.Null);
        Assert.That(cameras.GetComponentInChildren<SandboxQuitCallbackStatus>(true), Is.Null);
        Assert.That(cameras.GetComponentInChildren<SandboxDeveloperUtilityLayer>(true), Is.Null);
    }

    [Test]
    public void StandaloneGameplayMenuPrefabContainsNoSandboxDeveloperUtility()
    {
        GameObject menu = PrefabUtility.LoadPrefabContents(GameplayMenuPrefabPath);
        try
        {
            Assert.That(menu.GetComponentInChildren<SandboxDeveloperUtilityLayer>(true), Is.Null);
            Assert.That(menu.GetComponentInChildren<UISandboxController>(true), Is.Null);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(menu);
        }
    }

    [Test]
    public void SandboxSceneExistsAtTheCanonicalPathAndIsExcludedFromBuilds()
    {
        Assert.That(System.IO.File.Exists(SandboxScenePath), Is.True,
            "The Sandbox scene's canonical path is Assets/_Project/Scenes/UISandbox.unity.");

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == SandboxScenePath)
            {
                Assert.That(scene.enabled, Is.False, "The Sandbox must stay out of enabled Build Settings scenes.");
            }
        }
    }

    [Test]
    public void ProductionGearCatalogueContainsOnlyApprovedDefinitions()
    {
        GearDisplayCatalog catalog = AssetDatabase.LoadAssetAtPath<GearDisplayCatalog>(GearCatalogPath);
        Assert.That(catalog, Is.Not.Null);

        List<string> issues = new List<string>();
        catalog.CollectValidationIssues(issues);
        Assert.That(issues, Is.Empty, string.Join(" | ", issues));

        foreach (GearDisplayDefinition definition in catalog.Definitions)
        {
            Assert.That(definition, Is.Not.Null);
            string assetPath = AssetDatabase.GetAssetPath(definition);
            Assert.That(assetPath, Is.Not.Empty,
                "Production Gear definitions must be project assets, never runtime Sandbox fixtures.");
            Assert.That(definition.DisplayName, Is.Not.EqualTo(definition.RequiredAbility.ToString()),
                "A raw AbilityId name is not an approved physical Gear identity.");
        }
    }
}
