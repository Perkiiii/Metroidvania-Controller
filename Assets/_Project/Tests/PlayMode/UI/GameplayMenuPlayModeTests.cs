using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Real-player-loop coverage for Package A2: the production GameplayMenuScreen prefab registered as
/// the second pausing root, driven by actual Input System events through the real
/// <c>System/GameplayMenu</c>, <c>UI/PreviousTab</c>, <c>UI/NextTab</c>, and <c>UI/Cancel</c>
/// actions.
///
/// This instantiates the shipped prefab and the shipped Input Action asset rather than a fixture,
/// so a broken serialized reference fails here. Production types are reached by reflection because
/// this custom PlayMode assembly cannot reference the predefined Assembly-CSharp assembly.
///
/// Mouse tab-clicking and device hot-swap remain manual checks: they need a fully laid-out
/// screen-space canvas and physical devices.
/// </summary>
public sealed class GameplayMenuPlayModeTests
{
    private const string ScreenPrefabPath = "Assets/_Project/Prefabs/UI/GameplayMenuScreen.prefab";
    private const string PausePrefabPath = "Assets/_Project/Prefabs/UI/PauseMenuScreen.prefab";
    private const string InputActionsPath = "Assets/_Project/Input/InputSystem_Actions.inputactions";

    private static Assembly gameAssembly;

    private Keyboard keyboard;
    private Gamepad gamepad;
    private InputActionAsset actions;
    private GameObject gameManagerObject;
    private Component gameManager;
    private GameObject eventSystemObject;
    private EventSystem eventSystem;
    private GameObject canvasObject;
    private GameObject pauseRootObject;
    private Component pauseRoot;
    private GameObject menuInstance;
    private Component menuScreen;
    private GameObject flowObject;
    private Component flow;

    private static Type GameType(string name)
    {
        if (gameAssembly == null) gameAssembly = Assembly.Load("Assembly-CSharp");
        Type type = gameAssembly.GetType(name);
        Assert.That(type, Is.Not.Null, "Type not found in Assembly-CSharp: " + name);
        return type;
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, "Field not found: " + name);
        field.SetValue(target, value);
    }

    private static object GetProperty(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(property, Is.Not.Null, "Property not found: " + name);
        return property.GetValue(target);
    }

    private static object GetField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, "Field not found: " + name);
        return field.GetValue(target);
    }

    private static object Invoke(object target, string method, params object[] args)
    {
        foreach (MethodInfo candidate in target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (candidate.Name == method && candidate.GetParameters().Length == args.Length)
            {
                return candidate.Invoke(target, args);
            }
        }

        Assert.Fail("Method not found: " + method);
        return null;
    }

    private string ActiveTab()
    {
        return GetProperty(menuScreen, "ActiveTabId").ToString();
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        Time.timeScale = 1f;
        keyboard = InputSystem.AddDevice<Keyboard>();
        gamepad = InputSystem.AddDevice<Gamepad>();

        // The shipped action asset, so durable references on the prefab resolve exactly as they do
        // in a build. Every map this test enables is disabled again in TearDown.
        InputActionAsset shippedActions =
            UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        Assert.That(shippedActions, Is.Not.Null, "Could not load " + InputActionsPath);
        // InputAction state is mutable. Clone the shipped definitions per test so enabled maps,
        // interaction phases, and device resolution cannot leak between PlayMode cases.
        actions = InputActionAsset.FromJson(shippedActions.ToJson());

        gameManagerObject = new GameObject("A2 Test GameManager");
        gameManager = gameManagerObject.AddComponent(GameType("GameManager"));

        eventSystemObject = new GameObject("A2 Test EventSystem");
        eventSystem = eventSystemObject.AddComponent<EventSystem>();
        InputSystemUIInputModule module = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        module.actionsAsset = actions;

        canvasObject = new GameObject("A2 Test Canvas", typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.SetActive(false);

        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPrefabPath);
        Assert.That(prefab, Is.Not.Null, "Could not load " + ScreenPrefabPath);
        menuInstance = UnityEngine.Object.Instantiate(prefab, canvasObject.transform);
        menuScreen = menuInstance.GetComponent(GameType("GameplayMenuScreen"));
        Assert.That(menuScreen, Is.Not.Null, "The prefab has no GameplayMenuScreen component.");
        SetField(menuScreen, "eventSystem", eventSystem);
        SetField(menuScreen, "inputActions", actions);
        SetField(menuScreen, "previousTabActionRef", null);
        SetField(menuScreen, "nextTabActionRef", null);

        GameObject pausePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefabPath);
        Assert.That(pausePrefab, Is.Not.Null, "Could not load " + PausePrefabPath);
        pauseRootObject = UnityEngine.Object.Instantiate(pausePrefab, canvasObject.transform);
        pauseRoot = pauseRootObject.GetComponent(GameType("PauseMenuScreen"));
        Assert.That(pauseRoot, Is.Not.Null, "The shipped Pause prefab has no PauseMenuScreen component.");
        SetField(pauseRoot, "eventSystem", eventSystem);
        canvasObject.SetActive(true);

        flowObject = new GameObject("A2 Test UIFlowController");
        flowObject.SetActive(false);
        flow = flowObject.AddComponent(GameType("UIFlowController"));
        SetField(flow, "inputActions", actions);
        SetField(flow, "eventSystem", eventSystem);
        Invoke(flow, "ConfigureRoots", pauseRoot, menuScreen);
        flowObject.SetActive(true);
        SetField(flow, "lockoutRemaining", 0f);

        yield return ReleaseAllKeys();
#else
        yield return null;
        Assert.Ignore("Package A2 PlayMode coverage loads the shipped prefab through the AssetDatabase.");
#endif
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;

        if (flowObject != null) UnityEngine.Object.Destroy(flowObject);
        if (menuInstance != null) UnityEngine.Object.Destroy(menuInstance);
        if (canvasObject != null) UnityEngine.Object.Destroy(canvasObject);
        if (pauseRootObject != null) UnityEngine.Object.Destroy(pauseRootObject);
        if (eventSystemObject != null) UnityEngine.Object.Destroy(eventSystemObject);
        if (gameManagerObject != null) UnityEngine.Object.Destroy(gameManagerObject);

        // The shared production asset must not be left with maps enabled by this test.
        if (actions != null)
        {
            actions.Disable();
            UnityEngine.Object.Destroy(actions);
        }
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);

        yield return null;
    }

    // -------------------------------------------------------------------------
    // Root flow
    // -------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator ShippedPauseContinueControlClosesThroughUiFlowController()
    {
        yield return Press(Key.Escape);
        yield return ReleaseAllKeys();

        Button continueButton = GetField(pauseRoot, "continueButton") as Button;
        Assert.That(continueButton, Is.Not.Null);
        continueButton.onClick.Invoke();
        yield return null;

        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.False);
        Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Playing"));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(((GameObject)GetField(pauseRoot, "visualRoot")).activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator ShippedPauseToggleInputClosesTheBareRoot()
    {
        yield return Press(Key.Escape);
        yield return ReleaseAllKeys();
        yield return Press(Key.Escape);

        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.False);
        Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Playing"));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator ShippedPauseUiCancelClosesTheBareRoot()
    {
        yield return Press(Key.Escape);
        yield return ReleaseAllKeys();

        yield return Press(GamepadButton.East);

        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.False);
        Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Playing"));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator GameplayMenuKeyOpensTheRealRootAndPausesThroughGameManagerOnly()
    {
        yield return Press(Key.I);

        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.True);
        Assert.That(GetProperty(flow, "ActiveRootKind").ToString(), Does.Contain("GameplayMenu"));
        Assert.That((bool)GetProperty(menuScreen, "IsShown"), Is.True);
        Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Paused"));
        Assert.That(Time.timeScale, Is.EqualTo(0f));
        Assert.That(eventSystem.currentSelectedGameObject, Is.Not.Null,
            "Opening the root must establish a valid selection.");
    }

    [UnityTest]
    public IEnumerator AllFiveTabsAreVisibleAndReachableWhileOpen()
    {
        yield return Press(Key.I);
        yield return ReleaseAllKeys();

        string[] expected = { "Gear", "CombatLoadout", "Satchel", "FieldNotes", "Map" };
        string[] expectedDisplayNames = { "Gear", "Loadout", "Satchel", "Field Notes", "Map" };
        IList registrations = GetField(menuScreen, "tabs") as IList;
        Assert.That(registrations, Is.Not.Null);
        Assert.That(registrations.Count, Is.EqualTo(expected.Length));

        Button[] stripButtons = new Button[expected.Length];
        for (int i = 0; i < expected.Length; i++)
        {
            object registration = registrations[i];
            Assert.That(GetProperty(registration, "Id").ToString(), Is.EqualTo(expected[i]),
                $"Registration {i} is out of the confirmed order.");
            Assert.That(GetProperty(registration, "DisplayName"), Is.EqualTo(expectedDisplayNames[i]),
                $"Registration {i} has the wrong player-facing display name.");

            Component tabButton = GetProperty(registration, "TabButton") as Component;
            Assert.That(tabButton, Is.Not.Null, $"Tab '{expected[i]}' has no strip button.");
            stripButtons[i] = GetProperty(tabButton, "Button") as Button;
            Assert.That(stripButtons[i], Is.Not.Null, $"Tab '{expected[i]}' has no UGUI Button.");
            Assert.That(tabButton.gameObject.activeInHierarchy, Is.True, $"Tab '{expected[i]}' is not visible.");
            Assert.That(stripButtons[i].IsInteractable(), Is.True, $"Tab '{expected[i]}' is not reachable.");
        }

        for (int i = 0; i < stripButtons.Length; i++)
        {
            Button expectedLeft = stripButtons[(i - 1 + stripButtons.Length) % stripButtons.Length];
            Button expectedRight = stripButtons[(i + 1) % stripButtons.Length];
            Assert.That(stripButtons[i].navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(stripButtons[i].navigation.selectOnLeft, Is.SameAs(expectedLeft));
            Assert.That(stripButtons[i].navigation.selectOnRight, Is.SameAs(expectedRight));
        }
    }

    [UnityTest]
    public IEnumerator VisibleCloseControlClosesThroughUiFlowController()
    {
        yield return Press(Key.I);
        yield return ReleaseAllKeys();

        Button closeButton = GetField(menuScreen, "closeButton") as Button;
        Assert.That(closeButton, Is.Not.Null);
        closeButton.onClick.Invoke();
        yield return null;

        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.False);
        Assert.That((bool)GetProperty(menuScreen, "IsShown"), Is.False);
        Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Playing"));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator FreshGameplayMenuPressTogglesTheStableRootClosed()
    {
        yield return Press(Key.I);
        yield return ReleaseAllKeys();
        yield return Press(Key.I);

        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.False);
        Assert.That((bool)GetProperty(menuScreen, "IsShown"), Is.False);
        Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Playing"));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
    }

    [UnityTest]
    public IEnumerator CancelAtABareTabClosesTheWholeRoot()
    {
        yield return Press(Key.I);
        yield return ReleaseAllKeys();

        yield return Press(Key.Escape);

        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.False,
            "No tab handles Back in Package A2, so Cancel must close the root.");
        Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Playing"));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator PauseIsRejectedWhileTheGameplayMenuOwnsTheRoot()
    {
        yield return Press(Key.I);
        yield return ReleaseAllKeys();

        yield return Press(Key.Escape);

        Assert.That(((GameObject)GetField(pauseRoot, "visualRoot")).activeSelf, Is.False,
            "Escape must route to Gameplay Menu Back, never open Pause behind it.");
    }

    [UnityTest]
    public IEnumerator GameplayMenuIsRejectedWhilePauseOwnsTheRoot()
    {
        yield return Press(Key.Escape);
        yield return ReleaseAllKeys();
        Assert.That(GetProperty(flow, "ActiveRootKind").ToString(), Does.Contain("Pause"));

        yield return Press(Key.I);

        Assert.That(GetProperty(flow, "ActiveRootKind").ToString(), Does.Contain("Pause"),
            "Roots never switch directly; the other root's request is rejected.");
        Assert.That((bool)GetProperty(menuScreen, "IsShown"), Is.False);
    }

    // -------------------------------------------------------------------------
    // Tab cycling through the real UI actions
    // -------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator NextTabActionAdvancesThroughEveryTabInOrder()
    {
        yield return Press(Key.I);
        yield return ReleaseAllKeys();
        Assert.That(ActiveTab(), Is.EqualTo("Gear"));

        string[] expected = { "CombatLoadout", "Satchel", "FieldNotes", "Map" };
        foreach (string tab in expected)
        {
            yield return Press(Key.E);
            yield return ReleaseAllKeys();
            Assert.That(ActiveTab(), Is.EqualTo(tab));
        }
    }

    [UnityTest]
    public IEnumerator TabCyclingWrapsAtBothEnds()
    {
        yield return Press(Key.I);
        yield return ReleaseAllKeys();

        yield return Press(Key.Q);
        yield return ReleaseAllKeys();
        Assert.That(ActiveTab(), Is.EqualTo("Map"), "Previous from Gear wraps to Map.");

        yield return Press(Key.E);
        yield return ReleaseAllKeys();
        Assert.That(ActiveTab(), Is.EqualTo("Gear"), "Next from Map wraps to Gear.");
    }

    [UnityTest]
    public IEnumerator SelectionIsNeverNullWhileCyclingEveryTab()
    {
        yield return Press(Key.I);
        yield return ReleaseAllKeys();

        IList registrations = GetField(menuScreen, "tabs") as IList;
        Assert.That(registrations, Is.Not.Null);
        for (int i = 0; i < registrations.Count; i++)
        {
            Assert.That(eventSystem.currentSelectedGameObject, Is.Not.Null,
                $"Selection became null on '{ActiveTab()}'.");
            yield return Press(Key.E);
            yield return ReleaseAllKeys();
        }
    }

    [UnityTest]
    public IEnumerator TabCyclingKeysDoNothingWhileTheRootIsClosed()
    {
        yield return Press(Key.E);
        yield return ReleaseAllKeys();
        yield return Press(Key.Q);
        yield return ReleaseAllKeys();

        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.False);
        Assert.That(ActiveTab(), Is.EqualTo("Gear"),
            "Tab input while closed must be ignored, not queued.");
    }

    [UnityTest]
    public IEnumerator LastViewedTabIsRestoredOnReopen()
    {
        yield return Press(Key.I);
        yield return ReleaseAllKeys();
        yield return Press(Key.E);
        yield return ReleaseAllKeys();
        yield return Press(Key.E);
        yield return ReleaseAllKeys();
        Assert.That(ActiveTab(), Is.EqualTo("Satchel"));

        yield return Press(Key.I);
        yield return ReleaseAllKeys();
        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.False);

        yield return Press(Key.I);
        yield return ReleaseAllKeys();

        Assert.That(ActiveTab(), Is.EqualTo("Satchel"));
    }

    [UnityTest]
    public IEnumerator RepeatedOpenCloseCyclesLeaveNoResidualPauseOrSelection()
    {
        for (int i = 0; i < 4; i++)
        {
            yield return Press(Key.I);
            yield return ReleaseAllKeys();
            Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Paused"));

            yield return Press(Key.I);
            yield return ReleaseAllKeys();
            Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Playing"));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
        }
    }

    [UnityTest]
    public IEnumerator ClosedMenuPanelIsInactiveSoItCannotBlockHudRaycasts()
    {
        Transform panel = menuInstance.transform.Find("Panel");
        Assert.That(panel, Is.Not.Null);
        Assert.That(panel.gameObject.activeSelf, Is.False);

        yield return Press(Key.I);
        Assert.That(panel.gameObject.activeSelf, Is.True);

        yield return ReleaseAllKeys();
        yield return Press(Key.I);
        Assert.That(panel.gameObject.activeSelf, Is.False);
    }

    private IEnumerator Press(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null;
    }

    private IEnumerator Press(GamepadButton button)
    {
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button));
        yield return null;
    }

    private IEnumerator ReleaseAllKeys()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.QueueStateEvent(gamepad, new GamepadState());
        yield return null;
    }
}
